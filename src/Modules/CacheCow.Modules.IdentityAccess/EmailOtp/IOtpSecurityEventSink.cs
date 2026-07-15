using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CacheCow.Modules.IdentityAccess.EmailOtp;

/// <summary>
/// Structured security events for the OTP path (SECURITY.md, Logging rule 3;
/// issue 059, AC-08). Arguments are the pseudonymous account key
/// (<see cref="OtpAccountKey"/>) and the client address — never a raw email
/// address and never a code value (Logging rule 4; AC-07). Dedicated methods
/// per event mean call sites cannot improvise message text.
/// </summary>
public interface IOtpSecurityEventSink
{
    void CodeIssued(string accountKey);

    void IssuanceRateLimited(string accountKey, string clientAddress);

    void VerificationSucceeded(string accountKey);

    void VerificationFailed(string accountKey);

    void VerificationRateLimited(string accountKey, string clientAddress);

    void LockoutImposed(string accountKey);
}

/// <summary>
/// Default sink: compile-time LoggerMessage templates with named parameters
/// (never string interpolation into messages — SECURITY.md, Logging rule 4)
/// onto the host logging pipeline; centralized export to Azure Monitor rides
/// that pipeline (observability wiring is a later issue).
/// </summary>
public sealed class LoggerOtpSecurityEventSink : IOtpSecurityEventSink
{
    private static readonly Action<ILogger, string, Exception?> CodeIssuedMessage =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(2201, "OtpCodeIssued"),
            "Security event OtpCodeIssued: account {AccountKey}");

    private static readonly Action<ILogger, string, string, Exception?> IssuanceRateLimitedMessage =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(2202, "OtpIssuanceRateLimited"),
            "Security event OtpIssuanceRateLimited: account {AccountKey}, client {ClientAddress}");

    private static readonly Action<ILogger, string, Exception?> VerificationSucceededMessage =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(2203, "OtpVerificationSucceeded"),
            "Security event OtpVerificationSucceeded: account {AccountKey}");

    private static readonly Action<ILogger, string, Exception?> VerificationFailedMessage =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2204, "OtpVerificationFailed"),
            "Security event OtpVerificationFailed: account {AccountKey}");

    private static readonly Action<ILogger, string, string, Exception?> VerificationRateLimitedMessage =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(2205, "OtpVerificationRateLimited"),
            "Security event OtpVerificationRateLimited: account {AccountKey}, client {ClientAddress}");

    private static readonly Action<ILogger, string, Exception?> LockoutImposedMessage =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2206, "OtpLockoutImposed"),
            "Security event OtpLockoutImposed: account {AccountKey}");

    private readonly ILogger _logger;

    public LoggerOtpSecurityEventSink(ILoggerFactory? loggerFactory) =>
        _logger = loggerFactory?.CreateLogger("CacheCow.Modules.IdentityAccess.EmailOtp")
            ?? NullLogger.Instance;

    public void CodeIssued(string accountKey) => CodeIssuedMessage(_logger, accountKey, null);

    public void IssuanceRateLimited(string accountKey, string clientAddress) =>
        IssuanceRateLimitedMessage(_logger, accountKey, clientAddress, null);

    public void VerificationSucceeded(string accountKey) => VerificationSucceededMessage(_logger, accountKey, null);

    public void VerificationFailed(string accountKey) => VerificationFailedMessage(_logger, accountKey, null);

    public void VerificationRateLimited(string accountKey, string clientAddress) =>
        VerificationRateLimitedMessage(_logger, accountKey, clientAddress, null);

    public void LockoutImposed(string accountKey) => LockoutImposedMessage(_logger, accountKey, null);
}
