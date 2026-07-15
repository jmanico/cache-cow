using CacheCow.SharedKernel;

namespace CacheCow.Modules.Fulfillment.Serviceability;

/// <summary>
/// In-memory serviceable-postal-code sets, provisional until the operational
/// data source is decided (issue 045, Open Questions). Empty by default, so
/// with no supplied data nothing is serviceable — fail closed, never
/// optimistic (SECURITY.md, Logging rule 2).
/// </summary>
public sealed class InMemoryPostalCodeServiceabilitySource : IPostalCodeServiceabilitySource
{
    private readonly Dictionary<Market, HashSet<string>> _serviceableByMarket = [];

    public InMemoryPostalCodeServiceabilitySource(IEnumerable<ServiceablePostalCode> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        foreach (var entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry);

            if (!_serviceableByMarket.TryGetValue(entry.Market, out var postalCodes))
            {
                postalCodes = new HashSet<string>(StringComparer.Ordinal);
                _serviceableByMarket.Add(entry.Market, postalCodes);
            }

            postalCodes.Add(entry.PostalCode.Value);
        }
    }

    public bool IsServiceable(Market market, PostalCode postalCode) =>
        _serviceableByMarket.TryGetValue(market, out var postalCodes)
        && postalCodes.Contains(postalCode.Value);
}
