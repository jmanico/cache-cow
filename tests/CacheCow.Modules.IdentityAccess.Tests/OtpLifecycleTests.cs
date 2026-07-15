using CacheCow.Modules.IdentityAccess.EmailOtp;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.IdentityAccess.Tests;

/// <summary>
/// Issue 059 (CC-SEC-016, CC-SEC-005): OTP lifecycle — single-use codes,
/// supersession on newer issuance, and the 10-minute expiry boundary.
/// </summary>
public sealed class OtpLifecycleTests
{
    [Fact]
    [Requirement("CC-SEC-016")]
    [Requirement("CC-SEC-005")]
    public async Task Issued_code_verifies_once_for_an_existing_account()
    {
        var harness = new OtpHarness(OtpHarness.Options());

        var issuance = await harness.IssueAsync();
        var verification = await harness.VerifyAsync(harness.Dispatcher.LastCode);

        Assert.Equal(OtpIssuanceOutcome.Accepted, issuance.Outcome);
        Assert.Equal(OtpVerificationOutcome.Verified, verification.Outcome);
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public async Task Used_code_cannot_verify_a_second_time()
    {
        var harness = new OtpHarness(OtpHarness.Options());
        await harness.IssueAsync();
        var code = harness.Dispatcher.LastCode;

        var first = await harness.VerifyAsync(code);
        var replay = await harness.VerifyAsync(code);

        Assert.Equal(OtpVerificationOutcome.Verified, first.Outcome);
        Assert.Equal(OtpVerificationOutcome.Failed, replay.Outcome);
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public async Task Issuing_a_newer_code_invalidates_the_older_one()
    {
        var harness = new OtpHarness(OtpHarness.Options());

        await harness.IssueAsync();
        var older = harness.Dispatcher.LastCode;
        await harness.IssueAsync();
        var newer = harness.Dispatcher.LastCode;

        var oldResult = await harness.VerifyAsync(older);
        var newResult = await harness.VerifyAsync(newer);

        Assert.Equal(OtpVerificationOutcome.Failed, oldResult.Outcome);
        Assert.Equal(OtpVerificationOutcome.Verified, newResult.Outcome);
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public async Task Code_verifies_just_before_expiry_and_fails_at_the_boundary()
    {
        var lifetime = TimeSpan.FromMinutes(10);

        // Just before expiry: still valid.
        var fresh = new OtpHarness(OtpHarness.Options(codeLifetime: lifetime));
        await fresh.IssueAsync();
        fresh.Clock.Advance(lifetime - TimeSpan.FromSeconds(1));
        var beforeBoundary = await fresh.VerifyAsync(fresh.Dispatcher.LastCode);
        Assert.Equal(OtpVerificationOutcome.Verified, beforeBoundary.Outcome);

        // Exactly at expiry: already invalid (ExpiresAt is exclusive).
        var expired = new OtpHarness(OtpHarness.Options(codeLifetime: lifetime));
        await expired.IssueAsync();
        expired.Clock.Advance(lifetime);
        var atBoundary = await expired.VerifyAsync(expired.Dispatcher.LastCode);
        Assert.Equal(OtpVerificationOutcome.Failed, atBoundary.Outcome);
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public async Task Expired_code_stays_invalid_even_when_resubmitted_correctly()
    {
        var harness = new OtpHarness(OtpHarness.Options(codeLifetime: TimeSpan.FromMinutes(5)));
        await harness.IssueAsync();
        var code = harness.Dispatcher.LastCode;

        harness.Clock.Advance(TimeSpan.FromMinutes(6));

        Assert.Equal(OtpVerificationOutcome.Failed, (await harness.VerifyAsync(code)).Outcome);
        Assert.Equal(OtpVerificationOutcome.Failed, (await harness.VerifyAsync(code)).Outcome);
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public async Task Wrong_code_fails_and_correct_code_still_works_below_the_lockout_threshold()
    {
        var harness = new OtpHarness(OtpHarness.Options(maxVerificationFailures: 3));
        await harness.IssueAsync();
        var code = harness.Dispatcher.LastCode;
        var wrong = code == "000000" ? "000001" : "000000";

        var failed = await harness.VerifyAsync(wrong);
        var succeeded = await harness.VerifyAsync(code);

        Assert.Equal(OtpVerificationOutcome.Failed, failed.Outcome);
        Assert.Equal(OtpVerificationOutcome.Verified, succeeded.Outcome);
    }
}
