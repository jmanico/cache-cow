using System.Security.Cryptography;
using CacheCow.Modules.WholesaleB2B.Partners;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.WholesaleB2B.Orders;

/// <summary>
/// Consumer-facing-equivalent order states for the B2B surface, mirroring the
/// CC-ORD-006 vocabulary. Only <see cref="Received"/> is minted here; the
/// full transition machinery (with audit-logged transitions) is the Ordering
/// &amp; Payments context's authority — B2B orders surface state, they do not
/// own the state machine.
/// </summary>
public enum WholesaleOrderStatus
{
    Received = 0,
    Confirmed = 1,
    Packed = 2,
    Shipped = 3,
    Delivered = 4,
    Cancelled = 5,
    Refunded = 6,
}

/// <summary>One accepted case-quantity order line: SKU, case count, and the server-computed line total (CC-PRC-003/005).</summary>
public sealed record WholesaleOrderLine
{
    public WholesaleOrderLine(SkuId sku, int cases, Money lineTotal)
    {
        if (sku == default)
        {
            throw new WholesaleValidationException("A wholesale order line requires a SKU identity (CC-WHS-001).");
        }

        if (cases <= 0)
        {
            throw new WholesaleValidationException("A wholesale order line requires a positive case count (CC-WHS-001).");
        }

        Sku = sku;
        Cases = cases;
        LineTotal = lineTotal;
    }

    public SkuId Sku { get; }

    public int Cases { get; }

    /// <summary>Server-recomputed from the partner's own price list — never client-supplied (CC-PRC-005).</summary>
    public Money LineTotal { get; }
}

/// <summary>
/// A wholesale case-quantity order (CC-WHS-001) owned by exactly one partner
/// tenant — the scoping key of every read (CC-API-004; SECURITY.md,
/// Authentication rule 9). All money is server-computed at submission from the
/// partner's price list with overflow-checked integer-minor-unit arithmetic
/// (CC-PRC-003/005); client-supplied prices cannot exist because the request
/// contract has no price field.
/// </summary>
public sealed class WholesaleOrder
{
    private WholesaleOrder(
        string id,
        PartnerId owner,
        Market market,
        IReadOnlyList<WholesaleOrderLine> lines,
        Money total,
        DateTimeOffset createdAt)
    {
        Id = id;
        Owner = owner;
        Market = market;
        Lines = lines;
        Total = total;
        CreatedAt = createdAt;
    }

    public string Id { get; }

    public PartnerId Owner { get; }

    public Market Market { get; }

    public IReadOnlyList<WholesaleOrderLine> Lines { get; }

    public Money Total { get; }

    public WholesaleOrderStatus Status { get; } = WholesaleOrderStatus.Received;

    public DateTimeOffset CreatedAt { get; }

    public static WholesaleOrder Create(
        PartnerId owner,
        Market market,
        IReadOnlyList<WholesaleOrderLine> lines,
        Money total,
        DateTimeOffset createdAt)
    {
        if (owner == default)
        {
            throw new WholesaleValidationException("A wholesale order requires an owning partner tenant (CC-API-004).");
        }

        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0)
        {
            throw new WholesaleValidationException("A wholesale order requires at least one line (CC-WHS-001).");
        }

        return new WholesaleOrder(NewId(), owner, market, [.. lines], total, createdAt);
    }

    /// <summary>Unguessable order identifier (128 bits of cryptographic RNG) — never sequential or enumerable.</summary>
    private static string NewId() =>
        "wo_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
}
