using CacheCow.SharedKernel;

namespace CacheCow.Modules.Fulfillment.Serviceability;

/// <summary>
/// One row of serviceability data: <paramref name="PostalCode"/> is on
/// <paramref name="Market"/>'s serviceable set for frozen delivery (CC-FUL-002).
/// </summary>
public sealed record ServiceablePostalCode(Market Market, PostalCode PostalCode);
