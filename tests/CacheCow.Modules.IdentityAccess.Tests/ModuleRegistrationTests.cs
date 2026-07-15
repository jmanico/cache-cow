using CacheCow.Modules.IdentityAccess.EmailOtp;
using CacheCow.SharedKernel.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CacheCow.Modules.IdentityAccess.Tests;

/// <summary>
/// Module wiring: the bounded context registers the OTP hardening component
/// against its ports with provisional defaults; host registrations win
/// (TryAdd), and <see cref="EmailOtpOptions"/> is required host configuration
/// with no defaults (issue 059, Open Questions).
/// </summary>
public sealed class ModuleRegistrationTests
{
    private static EmailOtpOptions TestOptions() => OtpHarness.Options();

    [Fact]
    [Requirement("CC-SEC-016")]
    [Requirement("CC-SEC-005")]
    public void Module_resolves_the_otp_service_with_host_supplied_options()
    {
        var services = new ServiceCollection();
        services.AddSingleton(TestOptions()); // values are test fixtures, not decisions
        services.AddIdentityAccessModule();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<EmailOtpService>());
        Assert.IsType<InMemoryOtpStore>(provider.GetRequiredService<IOtpStore>());
        Assert.IsType<NullAccountDirectory>(provider.GetRequiredService<IAccountDirectory>());
        Assert.IsType<NullOtpDispatcher>(provider.GetRequiredService<IOtpDispatcher>());
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public void Host_registered_adapters_take_precedence_over_the_provisional_defaults()
    {
        var services = new ServiceCollection();
        var dispatcher = new RecordingDispatcher();
        var directory = new StubAccountDirectory("customer@example.com");
        services.AddSingleton(TestOptions());
        services.AddSingleton<IOtpDispatcher>(dispatcher);
        services.AddSingleton<IAccountDirectory>(directory);
        services.AddIdentityAccessModule();

        using var provider = services.BuildServiceProvider();

        Assert.Same(dispatcher, provider.GetRequiredService<IOtpDispatcher>());
        Assert.Same(directory, provider.GetRequiredService<IAccountDirectory>());
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public void Module_survives_host_boot_validation_even_before_options_are_configured()
    {
        var services = new ServiceCollection();
        services.AddIdentityAccessModule();

        // Host boot uses ValidateOnBuild: the deferred factory means the
        // missing (open-decision) options fail at first use, not at boot.
        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        Assert.NotNull(provider.GetRequiredService<IOtpStore>());
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public void Missing_required_options_fail_resolution_rather_than_defaulting()
    {
        var services = new ServiceCollection();
        services.AddIdentityAccessModule();

        using var provider = services.BuildServiceProvider();

        // No EmailOtpOptions were supplied: the numeric thresholds are an open
        // decision, so nothing may improvise them (CLAUDE.md working rules).
        Assert.Throws<InvalidOperationException>(provider.GetRequiredService<EmailOtpService>);
    }
}
