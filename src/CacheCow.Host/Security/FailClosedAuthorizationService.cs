using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace CacheCow.Host.Security;

/// <summary>
/// Fail-closed authorization: any exception thrown during authorization
/// evaluation (policy handler, tenancy/scope check, object-level check) is a
/// denial, never a bypass and never a 500 that differs from an ordinary
/// denial (SECURITY.md, Logging rule 2; issue 021 AC-03). The full exception
/// is logged server-side and a structured AuthorizationDenied security event
/// is emitted (SECURITY.md, Logging rule 3; issue 021 AC-06). Ordinary
/// (non-exceptional) denials are also emitted as security events.
/// </summary>
public sealed class FailClosedAuthorizationService : IAuthorizationService
{
    private static readonly Action<ILogger, Exception?> EvaluationFailedMessage =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(1101, "AuthorizationEvaluationFailed"),
            "Authorization evaluation threw; failing closed with a denial (SECURITY.md, Logging rule 2)");

    private readonly DefaultAuthorizationService _inner;
    private readonly ISecurityEventLogger _events;
    private readonly ILogger<FailClosedAuthorizationService> _logger;

    public FailClosedAuthorizationService(
        DefaultAuthorizationService inner,
        ISecurityEventLogger events,
        ILogger<FailClosedAuthorizationService> logger)
    {
        _inner = inner;
        _events = events;
        _logger = logger;
    }

    public async Task<AuthorizationResult> AuthorizeAsync(
        ClaimsPrincipal user,
        object? resource,
        IEnumerable<IAuthorizationRequirement> requirements)
    {
        AuthorizationResult result;
        try
        {
            result = await _inner.AuthorizeAsync(user, resource, requirements);
        }
#pragma warning disable CA1031 // Fail closed on *any* exception in the authorization path (SECURITY.md, Logging rule 2).
        catch (Exception exception)
#pragma warning restore CA1031
        {
            EvaluationFailedMessage(_logger, exception);
            _events.AuthorizationDenied(user?.Identity?.Name, resource?.ToString(), "evaluation-exception");
            return AuthorizationResult.Failed();
        }

        if (!result.Succeeded)
        {
            _events.AuthorizationDenied(user?.Identity?.Name, resource?.ToString(), "requirements-not-met");
        }

        return result;
    }

    public async Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
    {
        AuthorizationResult result;
        try
        {
            result = await _inner.AuthorizeAsync(user, resource, policyName);
        }
#pragma warning disable CA1031 // Fail closed on *any* exception in the authorization path (SECURITY.md, Logging rule 2).
        catch (Exception exception)
#pragma warning restore CA1031
        {
            EvaluationFailedMessage(_logger, exception);
            _events.AuthorizationDenied(user?.Identity?.Name, policyName, "evaluation-exception");
            return AuthorizationResult.Failed();
        }

        if (!result.Succeeded)
        {
            _events.AuthorizationDenied(user?.Identity?.Name, policyName, "requirements-not-met");
        }

        return result;
    }
}
