using CacheCow.Modules.BackOffice.Dashboard;
using CacheCow.Modules.BackOffice.Inventory;
using CacheCow.Modules.BackOffice.Orders;
using CacheCow.Modules.BackOffice.Partners;
using CacheCow.Modules.BackOffice.Rbac;
using CacheCow.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CacheCow.Modules.BackOffice.Api;

/// <summary>
/// The Back Office dashboard HTTP surface (issues 082, 084, 085) under
/// <see cref="DashboardApiSurface.BasePath"/>.
///
/// WHAT THIS LAYER DOES: mints the <see cref="StaffContext"/> from the
/// host-authenticated principal, reads and validates the request strictly,
/// calls the service, and maps the result onto an RFC 9457 response. It makes
/// NO authorization decision of its own — every permission check happens
/// server-side inside the services, through the single
/// <see cref="IDashboardAuthorizationService"/> enforcement point (CC-DSH-002;
/// SECURITY.md, Authentication rule 8). The descriptor's
/// <see cref="DashboardEndpointDescriptor.RequiredPermission"/> is declarative
/// metadata for tests and documentation — it is NOT the gate, so it cannot
/// drift into being a second, weaker one.
///
/// WHAT THE HOST MUST DO (this module never maps itself into a pipeline):
/// <list type="bullet">
/// <item>host these routes on the DASHBOARD ORIGIN only — separate origin from
/// storefront and portal, VPN-restricted, distinct session scope, no shared
/// cookies/tokens/modules (CC-SEC-011; SECURITY.md, HTTP boundary rule 8;
/// issue 084 AC-06, issue 085 AC-07);</item>
/// <item>authenticate staff by SSO with mandatory passkeys and run the
/// deny-by-default fallback authorization policy, so no route is reachable
/// unauthenticated (CC-DSH-001; SECURITY.md, Authentication rules 1–2; issue
/// 084 AC-05). <see cref="MapBackOfficeDashboard"/> calls
/// <c>RequireAuthorization()</c> on the group as defense in depth, but the
/// fallback policy is the host's;</item>
/// <item>implement step-up re-authentication (issue 060) and surface its
/// recency in the claim <see cref="DashboardClaimTypes.AuthenticationTime"/>,
/// which is what makes the refund gate real;</item>
/// <item>apply <c>[AutoValidateAntiforgeryToken]</c> to the cookie-authenticated
/// dashboard's state-changing requests (SECURITY.md, Authentication rule 11;
/// issue 085, Constraints) — CSRF is a host-pipeline concern;</item>
/// <item>register rate-limiter policies named
/// <see cref="DashboardRateLimitPolicies.Staff"/> and
/// <see cref="DashboardRateLimitPolicies.StaffCommands"/>, partitioned per
/// authenticated staff member, with 429 + Retry-After semantics (SECURITY.md,
/// HTTP boundary rule 7);</item>
/// <item>supply the port adapters: <see cref="IDashboardOrderReader"/> and
/// <see cref="IDashboardOrderCommands"/> (Ordering &amp; Payments),
/// <see cref="IDashboardInventoryReader"/> (Catalog &amp; Inventory), and
/// <see cref="IDashboardPartnerDirectory"/> / <see cref="IDashboardPartnerWorkflow"/>
/// (Wholesale &amp; B2B — mapping <see cref="DashboardActorReference"/> onto
/// that context's own actor proof). See <see cref="BackOfficeModule"/>.</item>
/// </list>
/// </summary>
public static class DashboardEndpoints
{
    /// <summary>Maps every Back Office dashboard endpoint (orders, inventory, partners).</summary>
    public static IEndpointRouteBuilder MapBackOfficeDashboard(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapDashboardOrders();
        endpoints.MapDashboardInventory();
        endpoints.MapDashboardPartners();
        return endpoints;
    }

    /// <summary>Issue 082: order search, CC-ORD-006 transitions, refunds.</summary>
    public static IEndpointRouteBuilder MapDashboardOrders(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = Group(endpoints);

        Map(group, DashboardApiSurface.OrderSearch, builder =>
            builder.MapGet(Relative(DashboardApiSurface.OrderSearch.Pattern), SearchOrders));
        Map(group, DashboardApiSurface.OrderTransition, builder =>
            builder.MapPost(Relative(DashboardApiSurface.OrderTransition.Pattern), (Delegate)TransitionOrderAsync));
        Map(group, DashboardApiSurface.OrderRefund, builder =>
            builder.MapPost(Relative(DashboardApiSurface.OrderRefund.Pattern), RefundOrder));

        return endpoints;
    }

    /// <summary>Issue 084: inventory by cold store (read-only).</summary>
    public static IEndpointRouteBuilder MapDashboardInventory(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = Group(endpoints);

        Map(group, DashboardApiSurface.InventorySearch, builder =>
            builder.MapGet(Relative(DashboardApiSurface.InventorySearch.Pattern), SearchInventory));

        return endpoints;
    }

    /// <summary>Issue 085: partner list/detail and the onboarding workflow actions.</summary>
    public static IEndpointRouteBuilder MapDashboardPartners(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var group = Group(endpoints);

        Map(group, DashboardApiSurface.PartnerSearch, builder =>
            builder.MapGet(Relative(DashboardApiSurface.PartnerSearch.Pattern), SearchPartners));
        Map(group, DashboardApiSurface.PartnerDetail, builder =>
            builder.MapGet(Relative(DashboardApiSurface.PartnerDetail.Pattern), GetPartner));
        Map(group, DashboardApiSurface.PartnerApprove, builder =>
            builder.MapPost(Relative(DashboardApiSurface.PartnerApprove.Pattern), ApprovePartner));
        Map(group, DashboardApiSurface.PartnerReject, builder =>
            builder.MapPost(Relative(DashboardApiSurface.PartnerReject.Pattern), RejectPartner));
        Map(group, DashboardApiSurface.PartnerSuspend, builder =>
            builder.MapPost(Relative(DashboardApiSurface.PartnerSuspend.Pattern), SuspendPartner));

        return endpoints;
    }

    private static RouteGroupBuilder Group(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(DashboardApiSurface.BasePath);

        // Defense in depth over the host's deny-by-default fallback policy
        // (SECURITY.md, Authentication rule 1; issue 084 AC-05).
        group.RequireAuthorization();
        group.RequireRateLimiting(DashboardRateLimitPolicies.Staff);

        // Every dashboard response is authenticated and staff-personalized:
        // never store it, never edge-cache it (SECURITY.md, HTTP boundary
        // rules 3 and 10; issue 084 AC-06, issue 085 AC-08). Set on the way
        // IN so it lands on error responses too, not only on success.
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });

        return group;
    }

    private static void Map(
        RouteGroupBuilder group,
        DashboardEndpointDescriptor descriptor,
        Func<RouteGroupBuilder, RouteHandlerBuilder> map)
    {
        var endpoint = map(group).WithMetadata(descriptor);

        // The stricter command policy overrides the group's default on
        // state-changing routes (SECURITY.md, HTTP boundary rule 7).
        if (!string.Equals(descriptor.RateLimitPolicy, DashboardRateLimitPolicies.Staff, StringComparison.Ordinal))
        {
            endpoint.RequireRateLimiting(descriptor.RateLimitPolicy);
        }
    }

    private static string Relative(string pattern) => pattern[DashboardApiSurface.BasePath.Length..];

    /// <summary>
    /// The authenticated staff context, or null. A null yields 401: no
    /// permission check ever runs against an actor whose identity or role
    /// could not be established (fail closed, SECURITY.md, Logging rule 2).
    /// </summary>
    private static StaffContext? StaffOf(HttpContext http) =>
        http.RequestServices.GetRequiredService<IStaffContextFactory>().Create(http.User);

    /// <summary>Correlation id for the audit record and the structured logs (SECURITY.md, Logging rule 1).</summary>
    private static string CorrelationOf(HttpContext http) => http.TraceIdentifier;

    // ---- issue 082 --------------------------------------------------------

    private static IResult SearchOrders(HttpContext http)
    {
        if (StaffOf(http) is not { } staff)
        {
            return DashboardProblems.Unauthorized();
        }

        var malformed = false;
        var request = http.Request;
        var market = DashboardRequestReader.OptionalMarket(request, "market", ref malformed);
        var stateName = DashboardRequestReader.Single(request, "state", ref malformed);
        var orderRef = DashboardRequestReader.Single(request, "orderRef", ref malformed);
        var placedFrom = DashboardRequestReader.OptionalTimestamp(request, "placedFrom", ref malformed);
        var placedTo = DashboardRequestReader.OptionalTimestamp(request, "placedTo", ref malformed);
        var page = DashboardRequestReader.OptionalInt(request, "page", ref malformed);
        var pageSize = DashboardRequestReader.OptionalInt(request, "pageSize", ref malformed);

        DashboardOrderState? state = null;
        if (stateName is not null)
        {
            if (!DashboardOrderStates.TryParse(stateName, out var parsed))
            {
                malformed = true;
            }
            else
            {
                state = parsed;
            }
        }

        if (malformed)
        {
            return DashboardProblems.Validation();
        }

        DashboardOrderSearchQuery query;
        try
        {
            query = DashboardOrderSearchQuery.Create(market, state, orderRef, placedFrom, placedTo, page, pageSize);
        }
        catch (DashboardValidationException)
        {
            return DashboardProblems.Validation();
        }

        var result = http.RequestServices.GetRequiredService<IDashboardOrderService>()
            .Search(staff, query, CorrelationOf(http));

        return DashboardProblems.From(result, page => Results.Ok(Project(page, ToResponse)));
    }

    private static async Task<IResult> TransitionOrderAsync(HttpContext http, string orderRef)
    {
        if (StaffOf(http) is not { } staff)
        {
            return DashboardProblems.Unauthorized();
        }

        var (body, rejection) = await DashboardRequestReader.ReadAsync<DashboardTransitionRequest>(http);
        if (rejection is not null)
        {
            return rejection;
        }

        // An unknown state name never reaches the service: the CC-ORD-006 set
        // is closed and resolution is exact (SECURITY.md, Input validation
        // rule 1). This is NOT a legality check — see IDashboardOrderCommands.
        if (!DashboardOrderStates.TryParse(body!.TargetState, out var targetState))
        {
            return DashboardProblems.Validation();
        }

        var result = http.RequestServices.GetRequiredService<IDashboardOrderService>()
            .Transition(staff, orderRef, targetState, CorrelationOf(http));

        return DashboardProblems.From(result, receipt => Results.Ok(
            new DashboardOrderTransitionResponse(receipt.OrderRef, DashboardOrderStates.NameOf(receipt.State))));
    }

    /// <summary>
    /// Refund. NO REQUEST BODY, deliberately: there is no client-supplied
    /// field a refund could legitimately carry. The amount is the Ordering
    /// context's canonical recomputation (CC-PRC-005; issue 082
    /// Anti-Patterns), and the actor comes from the session, never the
    /// request. Any body sent is ignored rather than parsed — an unread body
    /// cannot smuggle an amount.
    /// </summary>
    private static IResult RefundOrder(HttpContext http, string orderRef)
    {
        if (StaffOf(http) is not { } staff)
        {
            return DashboardProblems.Unauthorized();
        }

        var result = http.RequestServices.GetRequiredService<IDashboardOrderService>()
            .Refund(staff, orderRef, CorrelationOf(http));

        return DashboardProblems.From(result, receipt => Results.Ok(new DashboardOrderRefundResponse(
            receipt.OrderRef,
            DashboardOrderStates.NameOf(receipt.State),
            receipt.RefundedAmount.MinorUnits,
            receipt.RefundedAmount.Currency.Code)));
    }

    // ---- issue 084 --------------------------------------------------------

    private static IResult SearchInventory(HttpContext http)
    {
        if (StaffOf(http) is not { } staff)
        {
            return DashboardProblems.Unauthorized();
        }

        var malformed = false;
        var request = http.Request;
        var coldStoreId = DashboardRequestReader.Single(request, "coldStoreId", ref malformed);
        var market = DashboardRequestReader.OptionalMarket(request, "market", ref malformed);
        var skuValue = DashboardRequestReader.Single(request, "sku", ref malformed);
        var page = DashboardRequestReader.OptionalInt(request, "page", ref malformed);
        var pageSize = DashboardRequestReader.OptionalInt(request, "pageSize", ref malformed);

        SkuId? sku = null;
        if (skuValue is not null)
        {
            if (!SkuId.TryParse(skuValue, out var parsed))
            {
                malformed = true;
            }
            else
            {
                sku = parsed;
            }
        }

        if (malformed)
        {
            return DashboardProblems.Validation();
        }

        DashboardInventoryQuery query;
        try
        {
            query = DashboardInventoryQuery.Create(coldStoreId, market, sku, page, pageSize);
        }
        catch (DashboardValidationException)
        {
            return DashboardProblems.Validation();
        }

        var result = http.RequestServices.GetRequiredService<IDashboardInventoryService>()
            .Search(staff, query, CorrelationOf(http));

        return DashboardProblems.From(result, page => Results.Ok(Project(page, ToResponse)));
    }

    // ---- issue 085 --------------------------------------------------------

    private static IResult SearchPartners(HttpContext http)
    {
        if (StaffOf(http) is not { } staff)
        {
            return DashboardProblems.Unauthorized();
        }

        var malformed = false;
        var request = http.Request;
        var stateName = DashboardRequestReader.Single(request, "state", ref malformed);
        var page = DashboardRequestReader.OptionalInt(request, "page", ref malformed);
        var pageSize = DashboardRequestReader.OptionalInt(request, "pageSize", ref malformed);

        DashboardPartnerState? state = null;
        if (stateName is not null)
        {
            if (!Enum.TryParse<DashboardPartnerState>(stateName, ignoreCase: false, out var parsed)
                || !Enum.IsDefined(parsed))
            {
                malformed = true;
            }
            else
            {
                state = parsed;
            }
        }

        if (malformed)
        {
            return DashboardProblems.Validation();
        }

        DashboardPartnerQuery query;
        try
        {
            query = DashboardPartnerQuery.Create(state, page, pageSize);
        }
        catch (DashboardValidationException)
        {
            return DashboardProblems.Validation();
        }

        var result = http.RequestServices.GetRequiredService<IDashboardPartnerService>()
            .Search(staff, query, CorrelationOf(http));

        return DashboardProblems.From(result, page => Results.Ok(Project(page, ToResponse)));
    }

    private static IResult GetPartner(HttpContext http, string partnerId)
    {
        if (StaffOf(http) is not { } staff)
        {
            return DashboardProblems.Unauthorized();
        }

        var result = http.RequestServices.GetRequiredService<IDashboardPartnerService>()
            .Find(staff, partnerId, CorrelationOf(http));

        return DashboardProblems.From(result, detail => Results.Ok(ToResponse(detail)));
    }

    private static IResult ApprovePartner(HttpContext http, string partnerId) =>
        PartnerAction(http, partnerId, (service, staff, correlation) => service.Approve(staff, partnerId, correlation));

    private static IResult RejectPartner(HttpContext http, string partnerId) =>
        PartnerAction(http, partnerId, (service, staff, correlation) => service.Reject(staff, partnerId, correlation));

    private static IResult SuspendPartner(HttpContext http, string partnerId) =>
        PartnerAction(http, partnerId, (service, staff, correlation) => service.Suspend(staff, partnerId, correlation));

    /// <summary>
    /// The partner workflow actions take NO request body for the same reason
    /// refunds do not: the target state is the route, and the actor is the
    /// session. Nothing is left for a client to supply.
    /// </summary>
    private static IResult PartnerAction(
        HttpContext http,
        string partnerId,
        Func<IDashboardPartnerService, StaffContext, string, DashboardActionResult<DashboardPartnerReceipt>> invoke)
    {
        if (StaffOf(http) is not { } staff)
        {
            return DashboardProblems.Unauthorized();
        }

        var service = http.RequestServices.GetRequiredService<IDashboardPartnerService>();
        var result = invoke(service, staff, CorrelationOf(http));

        return DashboardProblems.From(result, receipt => Results.Ok(
            new DashboardPartnerActionResponse(receipt.PartnerId, receipt.State.ToString())));
    }

    // ---- projection -------------------------------------------------------

    private static DashboardPageResponse<TResponse> Project<TRow, TResponse>(
        DashboardPage<TRow> page, Func<TRow, TResponse> map) =>
        new([.. page.Items.Select(map)], page.Page, page.PageSize, page.TotalCount);

    private static DashboardOrderRowResponse ToResponse(DashboardOrderRow row) => new(
        row.OrderRef,
        row.Market.Code,
        DashboardOrderStates.NameOf(row.State),
        row.PlacedAt,
        row.Total.MinorUnits,
        row.Total.Currency.Code);

    private static DashboardInventoryRowResponse ToResponse(DashboardInventoryRow row) => new(
        row.ColdStoreId,
        row.Market.Code,
        row.Sku.Value,
        row.QuantityOnHand,
        row.StockState.ToString(),
        row.ServiceLevelBasisPoints);

    private static DashboardPartnerRowResponse ToResponse(DashboardPartnerRow row) =>
        new(row.PartnerId, row.LegalName, row.State.ToString());

    private static DashboardPartnerDetailResponse ToResponse(DashboardPartnerDetail detail) => new(
        detail.Summary.PartnerId,
        detail.Summary.LegalName,
        detail.Summary.State.ToString(),
        [.. detail.Identities.Select(identity =>
            new DashboardPartnerIdentityResponse(identity.Market.Code, identity.Kind, identity.Value))],
        detail.PaymentTermsNetDays);
}
