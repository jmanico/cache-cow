namespace CacheCow.Modules.WholesaleB2B.Api.Contracts;

/// <summary>
/// Request and response contracts for the /v1 B2B API. These records are the
/// SINGLE SOURCE OF TRUTH (CC-API-010; ARCHITECTURE.md, Dependency rule 7):
/// the published JSON Schemas are generated FROM them
/// (<see cref="Schema.B2BJsonSchemaGenerator"/>), request bodies are
/// deserialized INTO them under strict options that reject unknown members
/// (CC-API-006), and the API document is generated from the same types — no
/// hand-maintained parallel definition exists anywhere.
///
/// Requests are dedicated DTOs bound from the body only — never entity or
/// domain models — and carry NO server-controlled field: no price, no partner
/// id, no tenant, no status (SECURITY.md, Input validation rule 3; CC-PRC-005).
/// </summary>
public sealed record CreateWholesaleOrderRequest
{
    /// <summary>Launch-market code (e.g. "DE"); must be a market the partner's tenancy authorizes.</summary>
    public required string Market { get; init; }

    public required IReadOnlyList<WholesaleOrderLineRequest> Lines { get; init; }
}

public sealed record WholesaleOrderLineRequest
{
    public required string Sku { get; init; }

    /// <summary>Case count (wholesale ordering is case-quantity, CC-WHS-001).</summary>
    public required int Cases { get; init; }
}

public sealed record WholesaleCatalogLineResponse(
    string Sku,
    int CasePackSize,
    long PricePerCaseMinorUnits,
    string Currency);

public sealed record WholesaleCatalogResponse(
    string Market,
    string Currency,
    IReadOnlyList<WholesaleCatalogLineResponse> Lines);

public sealed record WholesaleOrderLineResponse(
    string Sku,
    int Cases,
    long LineTotalMinorUnits);

public sealed record WholesaleOrderResponse(
    string OrderId,
    string Market,
    string Currency,
    string Status,
    long TotalMinorUnits,
    IReadOnlyList<WholesaleOrderLineResponse> Lines,
    DateTimeOffset CreatedAt);

public sealed record WholesaleInvoiceResponse(
    string InvoiceId,
    string OrderId,
    string Currency,
    long TotalMinorUnits,
    string Status);
