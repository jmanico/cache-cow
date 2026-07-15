using CacheCow.Modules.IdentityAccess.EmailOtp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CacheCow.Modules.IdentityAccess;

/// <summary>
/// Registration entry point for the IdentityAccess bounded context
/// (ARCHITECTURE.md, "Server bounded contexts" 9). Issue 059 lands the
/// email-code (OTP) hardening component (CC-SEC-016; SECURITY.md,
/// Authentication and authorization rule 13); the broader consumer
/// authentication via Microsoft Entra External ID is issue 058 and plugs into
/// it through the <see cref="IAccountDirectory"/> port.
///
/// The host must additionally supply, from outside this module:
/// - <see cref="EmailOtpOptions"/> — required configuration with NO defaults:
///   the failed-attempt threshold, lockout duration, and rate-limit numbers
///   await human ratification (issue 059, Open Questions);
/// - production replacements for the provisional ports (TryAdd, so host
///   registrations win): a durable <see cref="IOtpStore"/> and distributed
///   <see cref="IOtpRateLimiter"/> (blocked on the open residency/write-region
///   decision, ARCHITECTURE.md "Known unknowns"), the Entra External ID
///   <see cref="IAccountDirectory"/> adapter (issue 058), and the Azure
///   Communication Services <see cref="IOtpDispatcher"/> adapter (later issue).
/// </summary>
public static class IdentityAccessModule
{
    public static IServiceCollection AddIdentityAccessModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IOtpStore, InMemoryOtpStore>();
        services.TryAddSingleton<IOtpRateLimiter>(provider =>
            new InMemoryOtpRateLimiter(provider.GetService<TimeProvider>()));
        services.TryAddSingleton<IAccountDirectory, NullAccountDirectory>();
        services.TryAddSingleton<IOtpDispatcher, NullOtpDispatcher>();
        services.TryAddSingleton<IOtpSecurityEventSink>(provider =>
            new LoggerOtpSecurityEventSink(provider.GetService<ILoggerFactory>()));

        // Factory deliberately deferred: EmailOtpOptions is required host
        // configuration with no defaults (open decision on the numeric
        // thresholds), so resolution fails at first use, not at host boot,
        // while no endpoint consumes the flow yet.
        services.TryAddSingleton(provider => new EmailOtpService(
            provider.GetRequiredService<IOtpStore>(),
            provider.GetRequiredService<IOtpRateLimiter>(),
            provider.GetRequiredService<IAccountDirectory>(),
            provider.GetRequiredService<IOtpDispatcher>(),
            provider.GetRequiredService<EmailOtpOptions>(),
            provider.GetService<IOtpSecurityEventSink>(),
            provider.GetService<TimeProvider>()));

        return services;
    }
}
