using CacheCow.Modules.IdentityAccess.EmailOtp;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CacheCow.Modules.IdentityAccess.Tests;

/// <summary>Deterministic, manually advanced clock (server-controlled time in tests without wall-clock flake).</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    internal ManualTimeProvider(DateTimeOffset start) => _utcNow = start;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    internal void Advance(TimeSpan by) => _utcNow += by;
}

/// <summary>Captures every dispatch request; the "mailbox" tests read issued codes from.</summary>
internal sealed class RecordingDispatcher : IOtpDispatcher
{
    internal List<OtpDispatchRequest> Requests { get; } = [];

    internal OtpDispatchRequest Last => Requests[^1];

    internal string LastCode => Requests[^1].Code.RevealForDelivery();

    public ValueTask DispatchAsync(OtpDispatchRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return ValueTask.CompletedTask;
    }
}

/// <summary>Directory double: only the configured addresses map to accounts.</summary>
internal sealed class StubAccountDirectory : IAccountDirectory
{
    private readonly HashSet<string> _known;

    internal StubAccountDirectory(params string[] knownAddresses) =>
        _known = new HashSet<string>(knownAddresses, StringComparer.OrdinalIgnoreCase);

    public ValueTask<bool> AccountExistsAsync(string emailAddress, CancellationToken cancellationToken) =>
        ValueTask.FromResult(_known.Contains(emailAddress));
}

/// <summary>Minimal capturing logger: records every rendered message and structured state value.</summary>
internal sealed class CapturingLogger : ILogger
{
    internal List<string> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add(formatter(state, exception));

        if (state is IReadOnlyList<KeyValuePair<string, object?>> values)
        {
            foreach (var pair in values)
            {
                Entries.Add($"{pair.Key}={pair.Value}");
            }
        }
    }
}

internal sealed class CapturingLoggerFactory : ILoggerFactory
{
    internal CapturingLogger Logger { get; } = new();

    public ILogger CreateLogger(string categoryName) => Logger;

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }
}

/// <summary>One wired-up OTP service with all state visible to the test.</summary>
internal sealed class OtpHarness
{
    internal static readonly DateTimeOffset T0 = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    internal const string KnownAddress = "customer@example.com";
    internal const string UnknownAddress = "stranger@example.com";
    internal const string ClientIp = "203.0.113.10";

    internal OtpHarness(EmailOtpOptions options)
    {
        Clock = new ManualTimeProvider(T0);
        Store = new InMemoryOtpStore();
        RateLimiter = new InMemoryOtpRateLimiter(Clock);
        Dispatcher = new RecordingDispatcher();
        LoggerFactory = new CapturingLoggerFactory();
        Service = new EmailOtpService(
            Store,
            RateLimiter,
            new StubAccountDirectory(KnownAddress),
            Dispatcher,
            options,
            new LoggerOtpSecurityEventSink(LoggerFactory),
            Clock);
    }

    internal ManualTimeProvider Clock { get; }

    internal InMemoryOtpStore Store { get; }

    internal InMemoryOtpRateLimiter RateLimiter { get; }

    internal RecordingDispatcher Dispatcher { get; }

    internal CapturingLoggerFactory LoggerFactory { get; }

    internal EmailOtpService Service { get; }

    /// <summary>Permissive defaults for tests that exercise one control at a time; not ratified values.</summary>
    internal static EmailOtpOptions Options(
        int codeLength = 6,
        TimeSpan? codeLifetime = null,
        int maxVerificationFailures = 3,
        TimeSpan? lockoutDuration = null,
        TimeSpan? rateLimitWindow = null,
        int issuanceLimitPerAccount = 100,
        int issuanceLimitPerIp = 100,
        int verificationLimitPerAccount = 100,
        int verificationLimitPerIp = 100) =>
        new(
            codeLength,
            codeLifetime ?? TimeSpan.FromMinutes(10),
            maxVerificationFailures,
            lockoutDuration ?? TimeSpan.FromMinutes(15),
            rateLimitWindow ?? TimeSpan.FromMinutes(1),
            issuanceLimitPerAccount,
            issuanceLimitPerIp,
            verificationLimitPerAccount,
            verificationLimitPerIp);

    internal Task<OtpIssuanceResult> IssueAsync(string email = KnownAddress, string ip = ClientIp) =>
        Service.RequestCodeAsync(email, ip, TestContext.Current.CancellationToken);

    internal Task<OtpVerificationResult> VerifyAsync(string code, string email = KnownAddress, string ip = ClientIp) =>
        Service.VerifyCodeAsync(email, code, ip, TestContext.Current.CancellationToken);
}
