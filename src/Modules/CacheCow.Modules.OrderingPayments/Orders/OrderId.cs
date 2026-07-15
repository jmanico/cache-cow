namespace CacheCow.Modules.OrderingPayments.Orders;

/// <summary>
/// Opaque order identity. Random (non-sequential, non-enumerable) by
/// construction — order identifiers must never be guessable handles to order
/// data (CC-ORD-010 context; access control itself is issue 042's capability
/// tokens, not this type).
/// </summary>
public readonly record struct OrderId(Guid Value)
{
    public static OrderId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
