using CacheCow.Modules.CatalogInventory.Catalog;

namespace CacheCow.Modules.CatalogInventory.Search;

/// <summary>
/// Search port of the Catalog &amp; Inventory context (CC-CAT-005/006). The
/// in-memory implementation is the domain reference; the confirmed engine —
/// PostgreSQL full-text search per market and per locale (ARCHITECTURE.md,
/// "Technology decisions") — replaces it behind this port in the persistence
/// issue, using parameterized `plainto_tsquery`/`websearch_to_tsquery`
/// construction only (SECURITY.md, Input validation rule 4).
/// </summary>
public interface ICatalogSearchService
{
    /// <summary>
    /// Executes a per-market, per-locale search. <paramref name="classificationPermitted"/>
    /// is the caller-supplied gating predicate: market gating is the Market &amp;
    /// Gating Policy context's single enforcement point (ARCHITECTURE.md,
    /// Dependency rule 1), so this module implements no market conditionals of
    /// its own (CC-MKT-006) — the composition root passes the gating context's
    /// decision in. SKUs whose classification the predicate denies are never in
    /// the candidate set (CC-MKT-003 parity via composition, CC-API-007). If
    /// the predicate throws, the failure propagates: an error, never ungated
    /// results (fail closed; SECURITY.md, Logging rule 2).
    /// </summary>
    IReadOnlyList<Sku> Search(CatalogSearchQuery query, Func<ProductClassification, bool> classificationPermitted);
}
