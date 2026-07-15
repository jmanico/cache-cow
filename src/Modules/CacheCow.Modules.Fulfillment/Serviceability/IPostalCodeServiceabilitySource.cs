using CacheCow.SharedKernel;

namespace CacheCow.Modules.Fulfillment.Serviceability;

/// <summary>
/// Port over the per-market serviceable-postal-code data (CC-FUL-002). The
/// sets themselves are operational data not defined in the specs — source,
/// update cadence, and ownership are open questions (issue 045; issue 023 also
/// flagged whether this data belongs to gating policy). Implementations answer
/// only from server-held data; an unknown postal code is not serviceable
/// (fail closed, SECURITY.md, Input validation rule 1).
/// </summary>
public interface IPostalCodeServiceabilitySource
{
    /// <summary>
    /// Whether the validated, normalized postal code is on the transacting
    /// market's serviceable set. Unknown means false, never optimistic.
    /// </summary>
    bool IsServiceable(Market market, PostalCode postalCode);
}
