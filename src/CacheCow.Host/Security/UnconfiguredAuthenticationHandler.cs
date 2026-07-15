using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace CacheCow.Host.Security;

/// <summary>
/// Scheme names for host authentication.
/// </summary>
public static class CacheCowAuthentication
{
    /// <summary>
    /// Placeholder default scheme registered until the real identity
    /// providers land (Microsoft Entra External ID for consumers, Entra ID for
    /// staff SSO and the B2B OAuth2 authorization server - ARCHITECTURE.md,
    /// "Authentication model"; issues 054/058/060). It never authenticates
    /// anyone and challenges with 401, so the deny-by-default fallback policy
    /// (SECURITY.md, Authentication rule 1) is enforceable from day one.
    /// </summary>
    public const string PlaceholderScheme = "CacheCow.Unconfigured";
}

/// <summary>
/// Never authenticates (<see cref="AuthenticateResult.NoResult"/>); base-class
/// behavior yields 401 on challenge and 403 on forbid. Deny by default: with
/// this as the only scheme, every endpoint under the fallback policy rejects
/// all traffic until a real provider is configured.
/// </summary>
internal sealed class UnconfiguredAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public UnconfiguredAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
        Task.FromResult(AuthenticateResult.NoResult());
}
