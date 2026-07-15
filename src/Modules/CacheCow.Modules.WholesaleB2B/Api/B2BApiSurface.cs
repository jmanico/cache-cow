using CacheCow.Modules.WholesaleB2B.Api.Contracts;
using CacheCow.Modules.WholesaleB2B.Auth;
using CacheCow.Modules.WholesaleB2B.RateLimits;

namespace CacheCow.Modules.WholesaleB2B.Api;

/// <summary>
/// One /v1 endpoint's complete published shape: route, method, required scope,
/// rate-limit policy, and the contract records its schemas generate from.
/// Descriptors are attached to the mapped endpoints as metadata AND drive the
/// generated API document, so the mapped surface and the documented surface
/// are the same object graph — they cannot diverge (CC-API-010;
/// ARCHITECTURE.md, Dependency rule 7).
/// </summary>
public sealed record B2BEndpointDescriptor(
    string Method,
    string Pattern,
    string RequiredScope,
    string RateLimitPolicy,
    Type? RequestContract,
    Type ResponseContract,
    bool RequiresIdempotencyKey,
    IReadOnlyList<string> RequirementIds);

/// <summary>
/// The versioned /v1 B2B API surface (CC-API-001): every endpoint lives under
/// <see cref="BasePath"/>; no unversioned route exists. Breaking changes
/// increment the version and v(n-1) stays supported for at least
/// <see cref="MinimumDeprecationWindowDays"/> days — the policy is published
/// inside the generated API document (issue 053, AC-02). Where the rendered
/// documentation is HOSTED is deliberately unresolved (issue 053, Open
/// Questions): the document is produced by a generation service, not served by
/// an endpoint.
/// </summary>
public static class B2BApiSurface
{
    public const string Version = "v1";
    public const string BasePath = "/v1";

    /// <summary>CC-API-001: at least 6 months.</summary>
    public const int MinimumDeprecationWindowDays = 183;

    public static B2BEndpointDescriptor CatalogList { get; } = new(
        "GET",
        BasePath + "/catalog/{market}",
        B2BScopes.CatalogRead,
        B2BRateLimitPolicies.Client,
        RequestContract: null,
        typeof(WholesaleCatalogResponse),
        RequiresIdempotencyKey: false,
        ["CC-API-001", "CC-API-004", "CC-API-007", "CC-WHS-001", "CC-WHS-003"]);

    public static B2BEndpointDescriptor CatalogItem { get; } = new(
        "GET",
        BasePath + "/catalog/{market}/{sku}",
        B2BScopes.CatalogRead,
        B2BRateLimitPolicies.Client,
        RequestContract: null,
        typeof(WholesaleCatalogLineResponse),
        RequiresIdempotencyKey: false,
        ["CC-API-001", "CC-API-004", "CC-API-007", "CC-MKT-004"]);

    public static B2BEndpointDescriptor OrderCreate { get; } = new(
        "POST",
        BasePath + "/orders",
        B2BScopes.OrdersWrite,
        B2BRateLimitPolicies.OrderCreation,
        typeof(CreateWholesaleOrderRequest),
        typeof(WholesaleOrderResponse),
        RequiresIdempotencyKey: true,
        ["CC-API-001", "CC-API-004", "CC-API-005", "CC-API-006", "CC-API-007", "CC-API-008", "CC-SEC-015"]);

    public static B2BEndpointDescriptor OrderRead { get; } = new(
        "GET",
        BasePath + "/orders/{orderId}",
        B2BScopes.OrdersRead,
        B2BRateLimitPolicies.Client,
        RequestContract: null,
        typeof(WholesaleOrderResponse),
        RequiresIdempotencyKey: false,
        ["CC-API-001", "CC-API-004", "CC-WHS-003"]);

    public static B2BEndpointDescriptor InvoiceRead { get; } = new(
        "GET",
        BasePath + "/invoices/{invoiceId}",
        B2BScopes.InvoicesRead,
        B2BRateLimitPolicies.Client,
        RequestContract: null,
        typeof(WholesaleInvoiceResponse),
        RequiresIdempotencyKey: false,
        ["CC-API-001", "CC-API-004", "CC-WHS-003", "CC-WHS-004"]);

    /// <summary>The complete published surface, in stable documentation order.</summary>
    public static IReadOnlyList<B2BEndpointDescriptor> Endpoints { get; } =
        [CatalogList, CatalogItem, OrderCreate, OrderRead, InvoiceRead];
}
