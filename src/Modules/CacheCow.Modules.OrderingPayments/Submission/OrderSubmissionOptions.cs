namespace CacheCow.Modules.OrderingPayments.Submission;

/// <summary>
/// Required, explicit submission bounds. Quantities are attacker-influenced
/// (CC-PRC-003), so a maximum is mandatory configuration — the specs name no
/// number, so there is deliberately no default value baked in here.
/// </summary>
public sealed class OrderSubmissionOptions
{
    /// <param name="maxQuantityPerLine">Inclusive upper bound for a single line's quantity; must be positive.</param>
    public OrderSubmissionOptions(int maxQuantityPerLine)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxQuantityPerLine, 0);
        MaxQuantityPerLine = maxQuantityPerLine;
    }

    public int MaxQuantityPerLine { get; }
}
