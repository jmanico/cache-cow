using CacheCow.Modules.WholesaleB2B.Partners;

namespace CacheCow.Modules.WholesaleB2B.Orders;

/// <summary>
/// Tenant-scoped wholesale order store. Mirrors the
/// <see cref="PriceLists.IWholesalePriceLists"/> pattern: every read requires
/// a <see cref="PartnerTenantContext"/> and resolves strictly through its own
/// <see cref="PartnerTenantContext.PartnerId"/>, so partner A cannot read or
/// even confirm the existence of partner B's orders — a miss and a
/// cross-tenant probe are both null, surfacing as 404 (CC-API-004;
/// SECURITY.md, Authentication rule 9; CC-QA-005).
/// </summary>
public interface IWholesaleOrders
{
    void Add(WholesaleOrder order);

    /// <summary>The context's own order, or null (not found and not-yours are indistinguishable).</summary>
    WholesaleOrder? Find(PartnerTenantContext context, string orderId);
}

/// <summary>
/// In-memory implementation until the durable PostgreSQL wholesale schema
/// lands (issue 015; SECURITY.md, Secret handling rule 10).
/// </summary>
public sealed class InMemoryWholesaleOrders : IWholesaleOrders
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, WholesaleOrder> _ordersById = new(StringComparer.Ordinal);

    public void Add(WholesaleOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);

        lock (_gate)
        {
            if (!_ordersById.TryAdd(order.Id, order))
            {
                throw new WholesaleValidationException("Duplicate wholesale order identifier.");
            }
        }
    }

    public WholesaleOrder? Find(PartnerTenantContext context, string orderId)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(orderId))
        {
            return null;
        }

        lock (_gate)
        {
            return _ordersById.TryGetValue(orderId, out var order) && order.Owner == context.PartnerId
                ? order
                : null;
        }
    }
}
