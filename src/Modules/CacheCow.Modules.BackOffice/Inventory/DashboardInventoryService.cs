using CacheCow.Modules.BackOffice.Dashboard;
using CacheCow.Modules.BackOffice.Rbac;

namespace CacheCow.Modules.BackOffice.Inventory;

/// <summary>
/// The inventory-by-cold-store module of the internal dashboard (issue 084;
/// CC-DSH-003, CC-CAT-002): a read-only view, gated by the
/// <c>inventory.view</c> permission.
/// </summary>
public interface IDashboardInventoryService
{
    /// <summary>Reads inventory rows (permission <c>inventory.view</c>).</summary>
    DashboardActionResult<DashboardPage<DashboardInventoryRow>> Search(
        StaffContext staff, DashboardInventoryQuery query, string correlationId);
}

/// <summary>
/// Inventory by cold store (issue 084). The module is a pure read: it holds no
/// mutation path, writes no audit event (there is no privileged action to
/// audit — CC-DSH-004 covers actions and state transitions), and derives
/// nothing. Availability states and the CC-DSH-006 service level both arrive
/// already computed from the Catalog &amp; Inventory context (AC-07;
/// ARCHITECTURE.md, Dependency rule 1).
///
/// Its whole security contribution is therefore the two rules below: permission
/// first, and never fabricate data.
/// </summary>
public sealed class DashboardInventoryService : IDashboardInventoryService
{
    private readonly IDashboardAuthorizationService authorization;
    private readonly IDashboardInventoryReader reader;

    public DashboardInventoryService(
        IDashboardAuthorizationService authorization,
        IDashboardInventoryReader reader)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        ArgumentNullException.ThrowIfNull(reader);

        this.authorization = authorization;
        this.reader = reader;
    }

    public DashboardActionResult<DashboardPage<DashboardInventoryRow>> Search(
        StaffContext staff, DashboardInventoryQuery query, string correlationId)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Fails closed with no matrix, an unknown role, or an ungranted
        // permission; the endpoint renders every denial as 404 (issue 084,
        // AC-04; SECURITY.md, Authentication rules 8–9).
        var decision = authorization.CheckPermission(staff, DashboardPermission.ViewInventory);
        if (!decision.IsGranted)
        {
            return DashboardActionResult.Denied<DashboardPage<DashboardInventoryRow>>(decision.Denial);
        }

        // A failed read is an error, never an empty grid: showing zero rows
        // for "the query failed" is a fabricated operational picture, and
        // inventory drives real fulfillment decisions (issue 084, Failure
        // Behavior; SECURITY.md, Logging rule 2).
#pragma warning disable CA1031
        try
        {
            return DashboardActionResult.Completed(reader.Search(query));
        }
        catch (Exception)
        {
            return DashboardActionResult.Unavailable<DashboardPage<DashboardInventoryRow>>();
        }
#pragma warning restore CA1031
    }
}
