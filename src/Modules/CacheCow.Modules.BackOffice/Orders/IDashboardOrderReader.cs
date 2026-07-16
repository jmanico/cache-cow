using CacheCow.Modules.BackOffice.Dashboard;

namespace CacheCow.Modules.BackOffice.Orders;

/// <summary>
/// The Back Office's READ port onto the Ordering &amp; Payments context
/// (issue 082; ARCHITECTURE.md, "Server bounded contexts" 4 and 8). The host
/// adapts this onto that context's module API — never onto its tables or
/// schema: each bounded context connects with its own least-privilege database
/// role confined to its own schema, so a cross-schema read is not merely poor
/// layering but a CC-SEC-021 violation (SECURITY.md, Secret handling rule 10).
/// This module references only the shared kernel (ARCHITECTURE.md, Dependency
/// rule 9), so the adapter lives in host composition.
///
/// Implementations MUST:
/// <list type="bullet">
/// <item>project only the PII-minimal fields of <see cref="DashboardOrderRow"/>
/// — the port is the place the customer PII stops, so an adapter must not
/// widen the row (issue 082, Data Classification);</item>
/// <item>use parameterized queries and never interpolate any field of the
/// query into SQL (SECURITY.md, Input validation rule 4);</item>
/// <item>return rows in a stable order — no client-chosen sort exists;</item>
/// <item>throw on failure rather than return an empty or partial page: the
/// caller fails closed and shows a generic error, and MUST NEVER render stale
/// or fabricated data (issue 084 Failure Behavior, the same rule applies
/// here).</item>
/// </list>
/// </summary>
public interface IDashboardOrderReader
{
    /// <summary>The matching page of orders. Throws if the read fails.</summary>
    DashboardPage<DashboardOrderRow> Search(DashboardOrderSearchQuery query);

    /// <summary>
    /// One order by reference, or null when it does not exist. Null yields a
    /// 404 — indistinguishable from the 404 an unauthorized caller receives
    /// (SECURITY.md, Authentication rule 9).
    /// </summary>
    DashboardOrderRow? Find(string orderRef);
}
