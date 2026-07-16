using CacheCow.SharedKernel;

namespace CacheCow.Modules.BackOffice.Orders;

/// <summary>
/// One PII-minimal order row for dashboard search results (issue 082, AC-01:
/// role-shaped fields only). The shape is deliberately closed to operational
/// fields — order reference, market, state, placement time, total — and
/// carries NO customer name, email, address, or payment detail: orders are
/// Restricted/PII data (issue 082, Data Classification) and the search grid
/// needs none of it. Detail-level customer data, if a role ever needs it, is
/// a separate, separately-authorized surface — not this row.
///
/// Monetary total is the shared integer-minor-unit <see cref="Money"/> type;
/// no floating point exists anywhere on this surface (CC-PRC-003).
/// </summary>
public sealed record DashboardOrderRow
{
    /// <summary>Maximum length of an order reference.</summary>
    public const int MaxOrderRefLength = 64;

    private DashboardOrderRow(string orderRef, Market market, DashboardOrderState state, DateTimeOffset placedAt, Money total)
    {
        OrderRef = orderRef;
        Market = market;
        State = state;
        PlacedAt = placedAt;
        Total = total;
    }

    /// <summary>The customer-facing order reference (monospace in the UI per DESIGN.md §1) — an identifier, not PII.</summary>
    public string OrderRef { get; }

    /// <summary>The transacting market of the order (CC-MKT-001).</summary>
    public Market Market { get; }

    /// <summary>Current CC-ORD-006 state, as reported by the Ordering context.</summary>
    public DashboardOrderState State { get; }

    /// <summary>When the order was placed (server-recorded by the Ordering context).</summary>
    public DateTimeOffset PlacedAt { get; }

    /// <summary>Order total in integer minor units (CC-PRC-003), computed by the Ordering context (CC-PRC-005).</summary>
    public Money Total { get; }

    public static DashboardOrderRow Create(
        string orderRef,
        Market market,
        DashboardOrderState state,
        DateTimeOffset placedAt,
        Money total)
    {
        ValidateOrderRef(orderRef);

        if (market == default)
        {
            throw new DashboardValidationException(
                "An order row requires an initialized launch market (CC-MKT-001).");
        }

        // NameOf rejects values outside the CC-ORD-006 closed set.
        _ = DashboardOrderStates.NameOf(state);

        // Money.Currency throws InvalidMoneyException for uninitialized values;
        // surface that as a validation failure of this row.
        try
        {
            _ = total.Currency;
        }
        catch (InvalidMoneyException exception)
        {
            throw new DashboardValidationException(
                $"An order row requires an initialized Money total (CC-PRC-003): {exception.Message}");
        }

        return new DashboardOrderRow(orderRef, market, state, placedAt, total);
    }

    /// <summary>
    /// Validates an order reference: required, bounded, control-character
    /// free (log-injection and header-injection vector — SECURITY.md,
    /// Logging rule 5). Rejected, never trimmed or coerced.
    /// </summary>
    public static void ValidateOrderRef(string orderRef)
    {
        if (string.IsNullOrWhiteSpace(orderRef) || orderRef.Length > MaxOrderRefLength)
        {
            throw new DashboardValidationException(
                $"An order reference is required and at most {MaxOrderRefLength} characters (issue 082; SECURITY.md, Input validation rule 1).");
        }

        foreach (var character in orderRef)
        {
            if (char.IsControl(character))
            {
                throw new DashboardValidationException(
                    "An order reference must not contain control characters (SECURITY.md, Logging rule 5).");
            }
        }
    }
}
