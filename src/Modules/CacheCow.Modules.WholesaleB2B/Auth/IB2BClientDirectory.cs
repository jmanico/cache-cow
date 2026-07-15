using CacheCow.Modules.WholesaleB2B.Partners;

namespace CacheCow.Modules.WholesaleB2B.Auth;

/// <summary>
/// Port mapping a validated OAuth2 client identifier to its partner tenancy
/// record (CC-API-002; SECURITY.md, Authentication rule 5). The host adapts
/// this to its Entra ID client registration data (ARCHITECTURE.md,
/// Authentication model); the module never sees raw credentials — only the
/// client id extracted from an already signature-validated principal.
///
/// Contract: return the tenant for a known, registered client id, or null for
/// anything else (unknown ids are a 401, never an error page). The returned
/// tenant's onboarding state is re-checked by the caller —
/// <see cref="PartnerTenantContext.ForApprovedTenant"/> fails closed for every
/// non-Approved state, so suspending a partner revokes API access on the next
/// request. Implementations MUST NOT log client ids alongside credentials or
/// token material (SECURITY.md, Logging rule 4).
/// </summary>
public interface IB2BClientDirectory
{
    PartnerTenant? FindByClientId(string clientId);
}
