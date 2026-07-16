using CacheCow.Modules.BackOffice.Dashboard;
using CacheCow.Modules.BackOffice.Rbac;

namespace CacheCow.Modules.BackOffice.Api;

/// <summary>
/// One dashboard endpoint's complete shape: route, method, the permission it
/// demands, its rate-limit policy, and the requirement IDs it serves.
/// Descriptors are attached to the mapped endpoints as metadata, so the mapped
/// surface and the declared surface are the same object graph and cannot
/// drift apart — the same discipline the B2B API uses (ARCHITECTURE.md,
/// Dependency rule 7).
/// </summary>
/// <param name="Method">HTTP method.</param>
/// <param name="Pattern">Full route pattern.</param>
/// <param name="RequiredPermission">The CC-DSH-002 permission checked server-side inside the service.</param>
/// <param name="RateLimitPolicy">The named policy the host must register (<see cref="DashboardRateLimitPolicies"/>).</param>
/// <param name="RequirementIds">The CC-* requirements this endpoint implements.</param>
public sealed record DashboardEndpointDescriptor(
    string Method,
    string Pattern,
    DashboardPermission RequiredPermission,
    string RateLimitPolicy,
    IReadOnlyList<string> RequirementIds);

/// <summary>
/// The internal dashboard's Back Office HTTP surface: order management (issue
/// 082), inventory by cold store (issue 084), and partner management (issue
/// 085) — the CC-DSH-003 launch modules this bounded context serves.
///
/// Everything under <see cref="BasePath"/> is dashboard-origin only: a
/// separate origin from the storefront and portal, VPN-restricted, with a
/// distinct session scope, sharing no cookies, tokens, or modules with them
/// (CC-SEC-011; SECURITY.md, HTTP boundary rule 8; ARCHITECTURE.md, Dependency
/// rule 4). That isolation is HOST composition — this module declares routes,
/// never where they are hosted.
/// </summary>
public static class DashboardApiSurface
{
    /// <summary>Every dashboard route lives under this prefix.</summary>
    public const string BasePath = "/dashboard";

    // ---- issue 082: order management (CC-DSH-003, CC-ORD-006, CC-DSH-004) --

    public static DashboardEndpointDescriptor OrderSearch { get; } = new(
        "GET",
        BasePath + "/orders",
        DashboardPermission.SearchOrders,
        DashboardRateLimitPolicies.Staff,
        ["CC-DSH-003", "CC-DSH-002"]);

    public static DashboardEndpointDescriptor OrderTransition { get; } = new(
        "POST",
        BasePath + "/orders/{orderRef}/transition",
        DashboardPermission.TransitionOrders,
        DashboardRateLimitPolicies.StaffCommands,
        ["CC-DSH-003", "CC-ORD-006", "CC-DSH-004"]);

    public static DashboardEndpointDescriptor OrderRefund { get; } = new(
        "POST",
        BasePath + "/orders/{orderRef}/refund",
        DashboardPermission.IssueRefunds,
        DashboardRateLimitPolicies.StaffCommands,
        ["CC-DSH-003", "CC-ORD-006", "CC-DSH-001", "CC-DSH-004", "CC-PRC-003"]);

    // ---- issue 084: inventory by cold store (CC-DSH-003, CC-CAT-002) -------

    public static DashboardEndpointDescriptor InventorySearch { get; } = new(
        "GET",
        BasePath + "/inventory",
        DashboardPermission.ViewInventory,
        DashboardRateLimitPolicies.Staff,
        ["CC-DSH-003", "CC-CAT-002", "CC-CAT-003", "CC-DSH-006"]);

    // ---- issue 085: partner management (CC-DSH-003, CC-WHS-002) -----------

    public static DashboardEndpointDescriptor PartnerSearch { get; } = new(
        "GET",
        BasePath + "/partners",
        DashboardPermission.ManagePartners,
        DashboardRateLimitPolicies.Staff,
        ["CC-DSH-003", "CC-WHS-002"]);

    public static DashboardEndpointDescriptor PartnerDetail { get; } = new(
        "GET",
        BasePath + "/partners/{partnerId}",
        DashboardPermission.ManagePartners,
        DashboardRateLimitPolicies.Staff,
        ["CC-DSH-003", "CC-WHS-002", "CC-WHS-004"]);

    public static DashboardEndpointDescriptor PartnerApprove { get; } = new(
        "POST",
        BasePath + "/partners/{partnerId}/approve",
        DashboardPermission.ApprovePartners,
        DashboardRateLimitPolicies.StaffCommands,
        ["CC-DSH-003", "CC-WHS-002", "CC-DSH-004"]);

    public static DashboardEndpointDescriptor PartnerReject { get; } = new(
        "POST",
        BasePath + "/partners/{partnerId}/reject",
        DashboardPermission.ApprovePartners,
        DashboardRateLimitPolicies.StaffCommands,
        ["CC-DSH-003", "CC-WHS-002", "CC-DSH-004"]);

    public static DashboardEndpointDescriptor PartnerSuspend { get; } = new(
        "POST",
        BasePath + "/partners/{partnerId}/suspend",
        DashboardPermission.ApprovePartners,
        DashboardRateLimitPolicies.StaffCommands,
        ["CC-DSH-003", "CC-WHS-002", "CC-DSH-004"]);

    /// <summary>The complete Back Office dashboard surface, in stable order.</summary>
    public static IReadOnlyList<DashboardEndpointDescriptor> Endpoints { get; } =
    [
        OrderSearch,
        OrderTransition,
        OrderRefund,
        InventorySearch,
        PartnerSearch,
        PartnerDetail,
        PartnerApprove,
        PartnerReject,
        PartnerSuspend,
    ];
}
