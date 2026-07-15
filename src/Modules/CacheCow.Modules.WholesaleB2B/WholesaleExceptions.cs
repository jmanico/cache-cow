using CacheCow.Modules.WholesaleB2B.Partners;

namespace CacheCow.Modules.WholesaleB2B;

/// <summary>
/// Base for all fail-closed wholesale/B2B failures in this bounded context
/// (CC-WHS-001–004; SECURITY.md, Logging rule 2: any exception in an
/// authorization or gating path is a denial, never a bypass).
/// </summary>
public abstract class WholesaleException : Exception
{
    protected WholesaleException(string message)
        : base(message)
    {
    }

    protected WholesaleException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Invalid wholesale input or configuration: rejected outright, never sanitized
/// or defaulted into acceptance (SECURITY.md, Input validation rule 1).
/// </summary>
public class WholesaleValidationException : WholesaleException
{
    public WholesaleValidationException(string message)
        : base(message)
    {
    }

    public WholesaleValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// A partner business-identity value failed its per-market schema validation
/// (CC-WHS-002, AC-03; SECURITY.md, Input validation rule 1). The message never
/// echoes the submitted identifier — tax registration numbers are classified
/// Confidential (issue 049, Data Classification) and must not leak into error
/// bodies or logs (SECURITY.md, Logging rules 1 and 4).
/// </summary>
public sealed class InvalidBusinessIdentityException : WholesaleException
{
    public InvalidBusinessIdentityException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// A partner tenancy state transition outside the ratified onboarding workflow
/// (CC-WHS-002). Raised for every skip, reversal, self-transition, or exit from
/// a terminal state; the tenant is unchanged (fail closed).
/// </summary>
public sealed class IllegalPartnerTransitionException : WholesaleException
{
    public IllegalPartnerTransitionException(PartnerOnboardingState from, PartnerOnboardingState to)
        : base($"Illegal partner tenancy transition {from} -> {to}; only the ratified onboarding workflow transitions exist (CC-WHS-002).")
    {
        From = from;
        To = to;
    }

    public PartnerOnboardingState From { get; }

    public PartnerOnboardingState To { get; }
}

/// <summary>
/// A wholesale access path was requested for a partner tenant that is not in
/// the Approved state (CC-WHS-002, AC-01/AC-05). Pending, rejected, and
/// suspended tenants have no reachable wholesale surface; a tenant whose state
/// cannot be resolved is treated as not approved (issue 049, Failure Behavior).
/// </summary>
public sealed class PartnerNotApprovedException : WholesaleException
{
    public PartnerNotApprovedException(PartnerOnboardingState state)
        : base($"Partner tenant is in state {state}, not Approved; wholesale access is denied and fails closed (CC-WHS-002).")
    {
    }
}

/// <summary>
/// No wholesale price list is served for the requesting tenant context and
/// market. Deliberately identical whether the list does not exist, belongs to
/// another partner, or the tenant is not authorized for the market — the HTTP
/// surface maps it to 404 without confirming resource existence (CC-WHS-003;
/// SECURITY.md, Authentication rule 9).
/// </summary>
public sealed class WholesalePriceListUnavailableException : WholesaleException
{
    public WholesalePriceListUnavailableException()
        : base("No wholesale price list is available for this partner context and market (CC-WHS-003; surfaces as 404, never confirming existence).")
    {
    }
}
