namespace CacheCow.Host.Security;

/// <summary>
/// First-party structured security-event logging abstraction (SECURITY.md,
/// Logging rules 3-5; CC-SEC-010, CC-NFR-003; issue 022). Every event class of
/// Logging rule 3 has a dedicated method so call sites cannot improvise
/// message text: templates are compile-time LoggerMessage definitions with
/// named parameters (never string interpolation), and every user-influenced
/// value passes through <see cref="LogSanitizer"/> before entering the entry.
/// Callers MUST NOT pass credentials, tokens, OTP codes, capability tokens or
/// PANs as arguments (Logging rule 4); PII goes through <see cref="PiiRedactor"/>
/// first. Centralized export to Azure Monitor rides the host logging pipeline
/// (observability wiring is issue 095).
/// </summary>
public interface ISecurityEventLogger
{
    void AuthenticationSuccess(string? actor);

    void AuthenticationFailure(string? actor, string? reason);

    void AuthorizationDenied(string? actor, string? resource, string? reason);

    void ValidationRejected(string rule, string? subject);

    void AdminAction(string? actor, string action, string? target);

    void RateLimitRejected(string? clientKey, string? endpoint);
}

public sealed class SecurityEventLogger : ISecurityEventLogger
{
    private static readonly Action<ILogger, string, Exception?> AuthenticationSuccessMessage =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1001, "AuthenticationSuccess"),
            "Security event AuthenticationSuccess: actor {Actor}");

    private static readonly Action<ILogger, string, string, Exception?> AuthenticationFailureMessage =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(1002, "AuthenticationFailure"),
            "Security event AuthenticationFailure: actor {Actor}, reason {Reason}");

    private static readonly Action<ILogger, string, string, string, Exception?> AuthorizationDeniedMessage =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Warning,
            new EventId(1003, "AuthorizationDenied"),
            "Security event AuthorizationDenied: actor {Actor}, resource {Resource}, reason {Reason}");

    private static readonly Action<ILogger, string, string, Exception?> ValidationRejectedMessage =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(1004, "ValidationRejected"),
            "Security event ValidationRejected: rule {Rule}, subject {Subject}");

    private static readonly Action<ILogger, string, string, string, Exception?> AdminActionMessage =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Information,
            new EventId(1005, "AdminAction"),
            "Security event AdminAction: actor {Actor}, action {Action}, target {Target}");

    private static readonly Action<ILogger, string, string, Exception?> RateLimitRejectedMessage =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(1006, "RateLimitRejected"),
            "Security event RateLimitRejected: client {ClientKey}, endpoint {Endpoint}");

    private readonly ILogger<SecurityEventLogger> _logger;

    public SecurityEventLogger(ILogger<SecurityEventLogger> logger)
    {
        _logger = logger;
    }

    public void AuthenticationSuccess(string? actor) =>
        AuthenticationSuccessMessage(_logger, LogSanitizer.Sanitize(actor), null);

    public void AuthenticationFailure(string? actor, string? reason) =>
        AuthenticationFailureMessage(_logger, LogSanitizer.Sanitize(actor), LogSanitizer.Sanitize(reason), null);

    public void AuthorizationDenied(string? actor, string? resource, string? reason) =>
        AuthorizationDeniedMessage(
            _logger,
            LogSanitizer.Sanitize(actor),
            LogSanitizer.Sanitize(resource),
            LogSanitizer.Sanitize(reason),
            null);

    public void ValidationRejected(string rule, string? subject) =>
        ValidationRejectedMessage(_logger, LogSanitizer.Sanitize(rule), LogSanitizer.Sanitize(subject), null);

    public void AdminAction(string? actor, string action, string? target) =>
        AdminActionMessage(
            _logger,
            LogSanitizer.Sanitize(actor),
            LogSanitizer.Sanitize(action),
            LogSanitizer.Sanitize(target),
            null);

    public void RateLimitRejected(string? clientKey, string? endpoint) =>
        RateLimitRejectedMessage(_logger, LogSanitizer.Sanitize(clientKey), LogSanitizer.Sanitize(endpoint), null);
}
