namespace CacheCow.Modules.BackOffice.Api;

/// <summary>
/// The dashboard endpoints' wire contracts (issue 082/084/085). Dedicated
/// records, never domain or entity types (SECURITY.md, Input validation
/// rule 3), so what the module holds and what it publishes can be changed
/// independently and nothing leaks by accident.
///
/// MONEY ON THE WIRE is always an integer minor-unit amount plus its currency
/// code — never a decimal string, and above all never a JSON floating-point
/// number (CC-PRC-003: binary floating point is banned for money everywhere,
/// tests included). Formatting for display is the client's, using
/// locale-aware formatting (CC-PRC-004; DESIGN.md 4.4) — the server never
/// hand-formats currency.
/// </summary>
/// <param name="Items">The page's rows.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Rows per page (already clamped).</param>
/// <param name="TotalCount">Total matching rows across all pages.</param>
public sealed record DashboardPageResponse<TRow>(
    IReadOnlyList<TRow> Items,
    int Page,
    int PageSize,
    int TotalCount);

/// <summary>
/// One order row (issue 082, AC-01). PII-minimal by construction — it mirrors
/// <see cref="Orders.DashboardOrderRow"/>, which carries no customer name,
/// email, address, or payment detail.
/// </summary>
/// <param name="OrderRef">Order reference (Plex Mono in the UI, DESIGN.md §12).</param>
/// <param name="Market">Launch market code (CC-MKT-001).</param>
/// <param name="State">CC-ORD-006 state name.</param>
/// <param name="PlacedAt">Server-recorded placement time.</param>
/// <param name="TotalMinorUnits">Order total in integer minor units (CC-PRC-003).</param>
/// <param name="Currency">ISO currency code of <paramref name="TotalMinorUnits"/>.</param>
public sealed record DashboardOrderRowResponse(
    string OrderRef,
    string Market,
    string State,
    DateTimeOffset PlacedAt,
    long TotalMinorUnits,
    string Currency);

/// <summary>A completed CC-ORD-006 transition (issue 082, AC-02).</summary>
public sealed record DashboardOrderTransitionResponse(string OrderRef, string State);

/// <summary>
/// A completed refund (issue 082, AC-05). The amount is an OUTPUT of the
/// Ordering context's canonical recomputation (CC-PRC-005) — the client never
/// sends one.
/// </summary>
public sealed record DashboardOrderRefundResponse(
    string OrderRef,
    string State,
    long RefundedMinorUnits,
    string Currency);

/// <summary>
/// The transition request body (issue 082). Exactly one field: the target
/// state. No state, actor, timestamp, or monetary field is accepted from the
/// client — all are server-controlled (SECURITY.md, Input validation rule 3).
/// Unknown members are rejected, not stripped (see <see cref="DashboardRequestReader"/>).
/// </summary>
/// <param name="TargetState">The requested CC-ORD-006 state name; legality is the state machine's ruling, not this contract's.</param>
public sealed record DashboardTransitionRequest(string? TargetState);

/// <summary>
/// One inventory row (issue 084, AC-01). Internal operational data; no PII, no
/// money.
/// </summary>
/// <param name="ColdStoreId">The regional cold store.</param>
/// <param name="Market">Launch market code.</param>
/// <param name="Sku">SKU identity (CC-CAT-001).</param>
/// <param name="QuantityOnHand">Units held (a count).</param>
/// <param name="StockState">The CC-CAT-003 state, derived by the Catalog &amp; Inventory context.</param>
/// <param name="ServiceLevelBasisPoints">
/// The CC-DSH-006 stock service level in basis points (0..10000), or null when
/// unavailable. An integer: exact, orderable, and never a float (see
/// <see cref="Inventory.DashboardInventoryRow.ServiceLevelBasisPoints"/>).
/// </param>
public sealed record DashboardInventoryRowResponse(
    string ColdStoreId,
    string Market,
    string Sku,
    long QuantityOnHand,
    string StockState,
    int? ServiceLevelBasisPoints);

/// <summary>One partner row (issue 085, AC-01). No partner contact PII (issue 085, Data Classification).</summary>
public sealed record DashboardPartnerRowResponse(string PartnerId, string LegalName, string State);

/// <summary>One per-market business identity (CC-WHS-002).</summary>
public sealed record DashboardPartnerIdentityResponse(string Market, string Kind, string Value);

/// <summary>A partner's detail view (issue 085, AC-01/AC-04).</summary>
/// <param name="PartnerId">Partner identity.</param>
/// <param name="LegalName">Registered business name.</param>
/// <param name="State">Onboarding state (CC-WHS-002).</param>
/// <param name="Identities">Per-market business identities.</param>
/// <param name="PaymentTermsNetDays">Wholesale payment terms in days (CC-WHS-004; net-60 default).</param>
public sealed record DashboardPartnerDetailResponse(
    string PartnerId,
    string LegalName,
    string State,
    IReadOnlyList<DashboardPartnerIdentityResponse> Identities,
    int PaymentTermsNetDays);

/// <summary>A completed partner workflow action (issue 085, AC-02).</summary>
public sealed record DashboardPartnerActionResponse(string PartnerId, string State);
