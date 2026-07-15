using CacheCow.Modules.IdentityAccess.EmailOtp;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.IdentityAccess.Tests;

/// <summary>
/// Issue 059 (CC-SEC-016), AC-04: backoff/lockout after a small configurable
/// number of failed verifications, plus per-account AND per-IP rate limits on
/// BOTH issuance and verification, each carrying a Retry-After.
/// </summary>
public sealed class OtpThrottlingTests
{
    private const string OtherIp = "198.51.100.7";

    private static async Task<string> WrongCodeFor(OtpHarness harness)
    {
        await harness.IssueAsync();
        return harness.Dispatcher.LastCode == "000000" ? "000001" : "000000";
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public async Task Lockout_engages_after_the_configured_number_of_failures_even_for_the_correct_code()
    {
        var harness = new OtpHarness(OtpHarness.Options(maxVerificationFailures: 3, lockoutDuration: TimeSpan.FromMinutes(15)));
        var wrong = await WrongCodeFor(harness);
        var correct = harness.Dispatcher.LastCode;

        Assert.Equal(OtpVerificationOutcome.Failed, (await harness.VerifyAsync(wrong)).Outcome);
        Assert.Equal(OtpVerificationOutcome.Failed, (await harness.VerifyAsync(wrong)).Outcome);
        Assert.Equal(OtpVerificationOutcome.Failed, (await harness.VerifyAsync(wrong)).Outcome);

        // Locked out now: even the correct code is refused, with a Retry-After.
        var locked = await harness.VerifyAsync(correct);
        Assert.Equal(OtpVerificationOutcome.RateLimited, locked.Outcome);
        Assert.NotNull(locked.RetryAfter);
        Assert.True(locked.RetryAfter > TimeSpan.Zero);
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public async Task Lockout_expires_and_a_freshly_issued_code_verifies_again()
    {
        var lockout = TimeSpan.FromMinutes(15);
        var harness = new OtpHarness(OtpHarness.Options(maxVerificationFailures: 2, lockoutDuration: lockout));
        var wrong = await WrongCodeFor(harness);

        await harness.VerifyAsync(wrong);
        await harness.VerifyAsync(wrong);
        Assert.Equal(OtpVerificationOutcome.RateLimited, (await harness.VerifyAsync(wrong)).Outcome);

        harness.Clock.Advance(lockout + TimeSpan.FromSeconds(1));

        await harness.IssueAsync();
        var result = await harness.VerifyAsync(harness.Dispatcher.LastCode);
        Assert.Equal(OtpVerificationOutcome.Verified, result.Outcome);
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public async Task Issuance_is_rate_limited_per_account()
    {
        var harness = new OtpHarness(OtpHarness.Options(issuanceLimitPerAccount: 2, rateLimitWindow: TimeSpan.FromMinutes(1)));

        Assert.Equal(OtpIssuanceOutcome.Accepted, (await harness.IssueAsync()).Outcome);
        Assert.Equal(OtpIssuanceOutcome.Accepted, (await harness.IssueAsync()).Outcome);

        // Same account from a different IP still trips the per-account limit.
        var limited = await harness.IssueAsync(ip: OtherIp);
        Assert.Equal(OtpIssuanceOutcome.RateLimited, limited.Outcome);
        Assert.True(limited.RetryAfter > TimeSpan.Zero);
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public async Task Issuance_is_rate_limited_per_ip_across_different_addresses()
    {
        var harness = new OtpHarness(OtpHarness.Options(issuanceLimitPerIp: 2, rateLimitWindow: TimeSpan.FromMinutes(1)));

        Assert.Equal(OtpIssuanceOutcome.Accepted, (await harness.IssueAsync(email: "a@example.com")).Outcome);
        Assert.Equal(OtpIssuanceOutcome.Accepted, (await harness.IssueAsync(email: "b@example.com")).Outcome);

        var limited = await harness.IssueAsync(email: "c@example.com");
        Assert.Equal(OtpIssuanceOutcome.RateLimited, limited.Outcome);
        Assert.True(limited.RetryAfter > TimeSpan.Zero);
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public async Task Verification_is_rate_limited_per_account()
    {
        var harness = new OtpHarness(OtpHarness.Options(verificationLimitPerAccount: 2, maxVerificationFailures: 50));
        var wrong = await WrongCodeFor(harness);

        await harness.VerifyAsync(wrong);
        await harness.VerifyAsync(wrong);

        var limited = await harness.VerifyAsync(wrong, ip: OtherIp);
        Assert.Equal(OtpVerificationOutcome.RateLimited, limited.Outcome);
        Assert.True(limited.RetryAfter > TimeSpan.Zero);
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public async Task Verification_is_rate_limited_per_ip_across_different_addresses()
    {
        var harness = new OtpHarness(OtpHarness.Options(verificationLimitPerIp: 2, maxVerificationFailures: 50));

        await harness.VerifyAsync("000000", email: "a@example.com");
        await harness.VerifyAsync("000000", email: "b@example.com");

        var limited = await harness.VerifyAsync("000000", email: "c@example.com");
        Assert.Equal(OtpVerificationOutcome.RateLimited, limited.Outcome);
        Assert.True(limited.RetryAfter > TimeSpan.Zero);
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public async Task Rate_limit_window_elapsing_restores_service()
    {
        var window = TimeSpan.FromMinutes(1);
        var harness = new OtpHarness(OtpHarness.Options(issuanceLimitPerAccount: 1, rateLimitWindow: window));

        Assert.Equal(OtpIssuanceOutcome.Accepted, (await harness.IssueAsync()).Outcome);
        Assert.Equal(OtpIssuanceOutcome.RateLimited, (await harness.IssueAsync()).Outcome);

        harness.Clock.Advance(window + TimeSpan.FromSeconds(1));

        Assert.Equal(OtpIssuanceOutcome.Accepted, (await harness.IssueAsync()).Outcome);
    }
}
