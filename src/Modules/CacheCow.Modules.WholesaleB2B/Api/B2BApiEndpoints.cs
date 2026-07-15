using System.Security.Cryptography;
using System.Text;
using CacheCow.Modules.WholesaleB2B.Api.Contracts;
using CacheCow.Modules.WholesaleB2B.Auth;
using CacheCow.Modules.WholesaleB2B.Gating;
using CacheCow.Modules.WholesaleB2B.Idempotency;
using CacheCow.Modules.WholesaleB2B.Invoices;
using CacheCow.Modules.WholesaleB2B.Orders;
using CacheCow.Modules.WholesaleB2B.PriceLists;
using CacheCow.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CacheCow.Modules.WholesaleB2B.Api;

/// <summary>
/// The versioned /v1 B2B API (CC-API-001–010): catalog read, order create,
/// order read, invoice read — every route under <see cref="B2BApiSurface.BasePath"/>,
/// every endpoint requiring authentication, its CC-API-004 scope, and strict
/// tenant scoping through the validated <see cref="B2BClientContext"/>.
/// Mapped from the same <see cref="B2BEndpointDescriptor"/>s that generate the
/// API document (CC-API-010). The HOST calls
/// <see cref="MapWholesaleB2BApi(IEndpointRouteBuilder)"/>; see
/// <see cref="WholesaleB2BModule"/> for the full host wiring contract
/// (JwtBearer per SECURITY.md Authentication rule 7, rate limiter policies,
/// port adapters).
/// </summary>
public static class B2BApiEndpoints
{
    private const string ClientContextItemKey = "CacheCow.WholesaleB2B.B2BClientContext";
    private const string IdempotencyKeyHeader = "Idempotency-Key";
    private const int MaxIdempotencyKeyLength = 256;
    private const int MaxOrderLines = 200;
    private const int MaxCasesPerLine = 100_000;

    public static IEndpointRouteBuilder MapWholesaleB2BApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup(B2BApiSurface.BasePath);

        // Deny by default (SECURITY.md, Authentication rule 1) plus the
        // B2B default per-client limiter policy (CC-API-008); the
        // order-creation endpoint overrides with its stricter policy below.
        group.RequireAuthorization();
        group.RequireRateLimiting(RateLimits.B2BRateLimitPolicies.Client);

        // Every request mints a B2BClientContext from the host-validated
        // principal; failures are a generic 401 (issue 054).
        group.AddEndpointFilter(MintClientContextAsync);

        Map(group, B2BApiSurface.CatalogList, builder =>
            builder.MapGet(Relative(B2BApiSurface.CatalogList.Pattern), GetCatalog));
        Map(group, B2BApiSurface.CatalogItem, builder =>
            builder.MapGet(Relative(B2BApiSurface.CatalogItem.Pattern), GetCatalogItem));
        Map(group, B2BApiSurface.OrderCreate, builder =>
            builder.MapPost(Relative(B2BApiSurface.OrderCreate.Pattern), (Delegate)CreateOrderAsync));
        Map(group, B2BApiSurface.OrderRead, builder =>
            builder.MapGet(Relative(B2BApiSurface.OrderRead.Pattern), GetOrder));
        Map(group, B2BApiSurface.InvoiceRead, builder =>
            builder.MapGet(Relative(B2BApiSurface.InvoiceRead.Pattern), GetInvoice));

        return endpoints;
    }

    /// <summary>Applies the descriptor's published contract to the mapped endpoint (scope filter, rate-limit policy, metadata).</summary>
    private static void Map(
        RouteGroupBuilder group,
        B2BEndpointDescriptor descriptor,
        Func<RouteGroupBuilder, RouteHandlerBuilder> map)
    {
        var endpoint = map(group)
            .AddEndpointFilter((context, next) => RequireScope(context, next, descriptor.RequiredScope))
            .WithMetadata(descriptor);

        if (!string.Equals(descriptor.RateLimitPolicy, RateLimits.B2BRateLimitPolicies.Client, StringComparison.Ordinal))
        {
            endpoint.RequireRateLimiting(descriptor.RateLimitPolicy);
        }
    }

    private static string Relative(string pattern) =>
        pattern[B2BApiSurface.BasePath.Length..];

    private static async ValueTask<object?> MintClientContextAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var http = context.HttpContext;

        // Credentials never travel in query strings (SECURITY.md,
        // Authentication rule 5); reject without reading the value.
        if (http.Request.Query.ContainsKey("access_token"))
        {
            return B2BProblems.Unauthorized();
        }

        // Wholesale responses are personalized per tenant: never cacheable
        // (CC-MKT-009; SECURITY.md, HTTP boundary rule 10).
        http.Response.Headers.CacheControl = "no-store";

        B2BTokenValidationResult result;
        try
        {
            result = http.RequestServices
                .GetRequiredService<IB2BTokenClaimsValidator>()
                .Validate(http.User);
        }
        catch (Exception)
        {
            // Fail closed (SECURITY.md, Logging rule 2).
            return B2BProblems.Unauthorized();
        }

        if (!result.Succeeded)
        {
            return B2BProblems.Unauthorized();
        }

        http.Items[ClientContextItemKey] = result.Context;
        return await next(context);
    }

    private static async ValueTask<object?> RequireScope(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next,
        string requiredScope)
    {
        var client = ClientOf(context.HttpContext);
        return client.HasScope(requiredScope)
            ? await next(context)
            : B2BProblems.MissingScope();
    }

    /// <summary>The context the mint filter stored; its absence is a pipeline defect and fails closed.</summary>
    private static B2BClientContext ClientOf(HttpContext http) =>
        http.Items[ClientContextItemKey] as B2BClientContext
        ?? throw new InvalidOperationException(
            "B2B client context missing; endpoint invoked outside MapWholesaleB2BApi (fail closed).");

    // ---- catalog:read -----------------------------------------------------

    private static IResult GetCatalog(HttpContext http, string market)
    {
        var client = ClientOf(http);
        if (!TryResolveAuthorizedMarket(client, market, out var transactingMarket))
        {
            return B2BProblems.NotFound();
        }

        WholesalePriceList priceList;
        try
        {
            priceList = http.RequestServices
                .GetRequiredService<IWholesalePriceLists>()
                .GetPriceList(client.Tenant, transactingMarket);
        }
        catch (WholesalePriceListUnavailableException)
        {
            return B2BProblems.NotFound();
        }

        var gating = http.RequestServices.GetRequiredService<IB2BGatingCheck>();
        var lines = new List<WholesaleCatalogLineResponse>();
        foreach (var line in priceList.Lines.OrderBy(l => l.Sku.Value, StringComparer.Ordinal))
        {
            B2BGatingDecision decision;
            try
            {
                decision = gating.EvaluateSku(transactingMarket, line.Sku);
            }
            catch (Exception)
            {
                // A gating fault must never leak a possibly-gated catalog:
                // the whole response fails closed (SECURITY.md, Logging
                // rule 2; CC-API-007).
                return B2BProblems.GatingUnavailable();
            }

            if (decision == B2BGatingDecision.Permitted)
            {
                lines.Add(new WholesaleCatalogLineResponse(
                    line.Sku.Value,
                    line.CasePackSize,
                    line.PricePerCase.MinorUnits,
                    line.PricePerCase.Currency.Code));
            }
        }

        return Results.Ok(new WholesaleCatalogResponse(
            transactingMarket.Code,
            WholesaleMarketCurrencies.CurrencyOf(transactingMarket).Code,
            lines));
    }

    private static IResult GetCatalogItem(HttpContext http, string market, string sku)
    {
        var client = ClientOf(http);
        if (!TryResolveAuthorizedMarket(client, market, out var transactingMarket)
            || !SkuId.TryParse(sku, out var skuId))
        {
            return B2BProblems.NotFound();
        }

        WholesalePriceList priceList;
        try
        {
            priceList = http.RequestServices
                .GetRequiredService<IWholesalePriceLists>()
                .GetPriceList(client.Tenant, transactingMarket);
        }
        catch (WholesalePriceListUnavailableException)
        {
            return B2BProblems.NotFound();
        }

        if (!priceList.TryGetLine(skuId, out var line) || line is null)
        {
            return B2BProblems.NotFound();
        }

        // Gated, unknown, and gating-fault all present as the same 404 —
        // CC-MKT-004 semantics, no existence confirmation (SECURITY.md,
        // Authentication rule 9).
        try
        {
            if (http.RequestServices.GetRequiredService<IB2BGatingCheck>()
                    .EvaluateSku(transactingMarket, skuId) != B2BGatingDecision.Permitted)
            {
                return B2BProblems.NotFound();
            }
        }
        catch (Exception)
        {
            return B2BProblems.NotFound();
        }

        return Results.Ok(new WholesaleCatalogLineResponse(
            line.Sku.Value,
            line.CasePackSize,
            line.PricePerCase.MinorUnits,
            line.PricePerCase.Currency.Code));
    }

    // ---- orders:write -----------------------------------------------------

    private static async Task<IResult> CreateOrderAsync(HttpContext http)
    {
        var client = ClientOf(http);

        // CC-API-005: order creation REQUIRES an Idempotency-Key header.
        var keyValues = http.Request.Headers[IdempotencyKeyHeader];
        if (keyValues.Count != 1
            || keyValues[0] is not { } idempotencyKey
            || string.IsNullOrWhiteSpace(idempotencyKey)
            || idempotencyKey.Length > MaxIdempotencyKeyLength)
        {
            return B2BProblems.Validation(
                "Order creation requires exactly one non-empty Idempotency-Key header (CC-API-005).");
        }

        var (request, rejection) = await B2BRequestReader.ReadAsync<CreateWholesaleOrderRequest>(http);
        if (rejection is not null || request is null)
        {
            return rejection ?? B2BProblems.SchemaViolation();
        }

        if (!Market.TryParse(request.Market, out var market))
        {
            return B2BProblems.Validation("Unknown market code (CC-MKT-001).");
        }

        if (!client.Tenant.IsAuthorizedFor(market))
        {
            // Existence-hiding denial, consistent with every other
            // tenant-scoped miss (SECURITY.md, Authentication rule 9).
            return B2BProblems.NotFound();
        }

        if (request.Lines is not { Count: > 0 and <= MaxOrderLines })
        {
            return B2BProblems.Validation(
                $"An order carries between 1 and {MaxOrderLines} lines (CC-WHS-001).");
        }

        WholesalePriceList priceList;
        try
        {
            priceList = http.RequestServices
                .GetRequiredService<IWholesalePriceLists>()
                .GetPriceList(client.Tenant, market);
        }
        catch (WholesalePriceListUnavailableException)
        {
            return B2BProblems.NotFound();
        }

        // Validate and price every line BEFORE claiming the idempotency key,
        // so a rejected request never occupies the key. Server recomputes all
        // money from the canonical price list (CC-PRC-005); gating parity per
        // line (CC-API-007) fails closed, without enumerating the gated
        // catalog (issue 055, Failure Behavior).
        var gating = http.RequestServices.GetRequiredService<IB2BGatingCheck>();
        var orderLines = new List<WholesaleOrderLine>(request.Lines.Count);
        Money? total = null;
        foreach (var line in request.Lines)
        {
            if (!SkuId.TryParse(line.Sku, out var skuId)
                || line.Cases is <= 0 or > MaxCasesPerLine)
            {
                return B2BProblems.Validation("One or more order lines are invalid (CC-WHS-001).");
            }

            if (!priceList.TryGetLine(skuId, out var priceLine) || priceLine is null)
            {
                return B2BProblems.Validation("One or more order lines are not orderable in this market.");
            }

            try
            {
                if (gating.EvaluateSku(market, skuId) != B2BGatingDecision.Permitted)
                {
                    return B2BProblems.Validation("One or more order lines are not orderable in this market.");
                }
            }
            catch (Exception)
            {
                // Gating fault = denial, never an order (CC-API-007, AC-07).
                return B2BProblems.Validation("One or more order lines are not orderable in this market.");
            }

            Money lineTotal;
            try
            {
                lineTotal = priceLine.ExtendedPrice(line.Cases);
                total = total is { } sum ? sum.Add(lineTotal) : lineTotal;
            }
            catch (MoneyOverflowException)
            {
                // Overflow-checked money fails closed (CC-PRC-003).
                return B2BProblems.Validation("One or more order lines are invalid (CC-WHS-001).");
            }

            orderLines.Add(new WholesaleOrderLine(skuId, line.Cases, lineTotal));
        }

        // Idempotency: scoped to the authenticated client, bound to the
        // request fingerprint (CC-API-005, CC-SEC-015).
        var idempotency = http.RequestServices.GetRequiredService<IB2BOrderIdempotency>();
        var orders = http.RequestServices.GetRequiredService<IWholesaleOrders>();
        var fingerprint = Fingerprint(market, request.Lines);

        var claim = idempotency.Claim(client.ClientId, idempotencyKey, fingerprint);
        switch (claim.Status)
        {
            case B2BIdempotencyStatus.FingerprintConflict:
                return B2BProblems.IdempotencyConflict();

            case B2BIdempotencyStatus.Replay:
                var stored = orders.Find(client.Tenant, claim.StoredOrderId!);
                return stored is null
                    ? B2BProblems.NotFound()
                    : Results.Created(OrderLocation(stored.Id), ToResponse(stored));

            case B2BIdempotencyStatus.Accepted:
            default:
                break;
        }

        try
        {
            var timeProvider = http.RequestServices.GetService<TimeProvider>() ?? TimeProvider.System;
            var order = WholesaleOrder.Create(
                client.Tenant.PartnerId, market, orderLines, total!.Value, timeProvider.GetUtcNow());
            orders.Add(order);
            idempotency.Complete(client.ClientId, idempotencyKey, fingerprint, order.Id);
            return Results.Created(OrderLocation(order.Id), ToResponse(order));
        }
        catch (Exception)
        {
            // Free the key so a legitimate retry can run; the failure itself
            // propagates as a generic 500 problem via the host pipeline.
            idempotency.Release(client.ClientId, idempotencyKey);
            throw;
        }
    }

    // ---- orders:read / invoices:read ---------------------------------------

    private static IResult GetOrder(HttpContext http, string orderId)
    {
        var client = ClientOf(http);
        var order = http.RequestServices
            .GetRequiredService<IWholesaleOrders>()
            .Find(client.Tenant, orderId);
        return order is null ? B2BProblems.NotFound() : Results.Ok(ToResponse(order));
    }

    private static IResult GetInvoice(HttpContext http, string invoiceId)
    {
        var client = ClientOf(http);
        var invoice = http.RequestServices
            .GetRequiredService<IWholesaleInvoiceReader>()
            .FindInvoice(client.Tenant, invoiceId);
        return invoice is null
            ? B2BProblems.NotFound()
            : Results.Ok(new WholesaleInvoiceResponse(
                invoice.InvoiceId,
                invoice.OrderId,
                invoice.CurrencyCode,
                invoice.TotalMinorUnits,
                invoice.Status));
    }

    // ---- helpers ------------------------------------------------------------

    private static bool TryResolveAuthorizedMarket(B2BClientContext client, string marketCode, out Market market) =>
        Market.TryParse(marketCode, out market) && client.Tenant.IsAuthorizedFor(market);

    private static string OrderLocation(string orderId) =>
        $"{B2BApiSurface.BasePath}/orders/{orderId}";

    private static WholesaleOrderResponse ToResponse(WholesaleOrder order) =>
        new(
            order.Id,
            order.Market.Code,
            order.Total.Currency.Code,
            order.Status.ToString(),
            order.Total.MinorUnits,
            [.. order.Lines.Select(l => new WholesaleOrderLineResponse(l.Sku.Value, l.Cases, l.LineTotal.MinorUnits))],
            order.CreatedAt);

    /// <summary>
    /// Canonical fingerprint of the semantic request (CC-SEC-015): the same
    /// body always fingerprints identically; any change to market, SKUs,
    /// quantities, or line order does not.
    /// </summary>
    private static string Fingerprint(Market market, IReadOnlyList<WholesaleOrderLineRequest> lines)
    {
        var builder = new StringBuilder(market.Code);
        foreach (var line in lines)
        {
            builder.Append('\n')
                .Append(line.Sku)
                .Append('|')
                .Append(line.Cases.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }
}
