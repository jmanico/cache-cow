using CacheCow.Modules.IdentityAccess.EmailOtp;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.IdentityAccess.Tests;

/// <summary>
/// Issue 059 (CC-SEC-016), AC-06: responses are identical whether or not the
/// address maps to an account — issuance returns the same singleton result,
/// every verification failure returns the same singleton result, the send
/// decision lives behind the dispatch port, and unknown addresses accrue
/// rate-limit and lockout state exactly like known ones (same code path).
/// </summary>
public sealed class OtpEnumerationSafetyTests
{
    [Fact]
    [Requirement("CC-SEC-016")]
    public async Task Issuance_returns_the_identical_result_for_known_and_unknown_addresses()
    {
        var harness = new OtpHarness(OtpHarness.Options());

        var known = await harness.IssueAsync(OtpHarness.KnownAddress);
        var unknown = await harness.IssueAsync(OtpHarness.UnknownAddress);

        // Same singleton object: byte-identical at any serialization boundary.
        Assert.Same(known, unknown);
        Assert.Equal(OtpIssuanceOutcome.Accepted, unknown.Outcome);
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public async Task Dispatch_port_is_invoked_for_both_populations_and_carries_the_send_decision_flag()
    {
        var harness = new OtpHarness(OtpHarness.Options());

        await harness.IssueAsync(OtpHarness.KnownAddress);
        await harness.IssueAsync(OtpHarness.UnknownAddress);

        // The service always dispatches; whether mail is actually sent is the
        // adapter's decision, behind the port.
        Assert.Equal(2, harness.Dispatcher.Requests.Count);
        Assert.True(harness.Dispatcher.Requests[0].AccountExists);
        Assert.False(harness.Dispatcher.Requests[1].AccountExists);
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public async Task Verification_failure_is_the_identical_result_for_known_and_unknown_addresses()
    {
        var harness = new OtpHarness(OtpHarness.Options());
        await harness.IssueAsync(OtpHarness.KnownAddress);
        await harness.IssueAsync(OtpHarness.UnknownAddress);

        var knownFailure = await harness.VerifyAsync("000000", OtpHarness.KnownAddress);
        var unknownFailure = await harness.VerifyAsync("000000", OtpHarness.UnknownAddress);
        var neverIssuedFailure = await harness.VerifyAsync("000000", "nobody@example.com");

        // Wrong code, wrong code for a non-account, and no-code-outstanding
        // all collapse to one singleton: nothing distinguishes the reasons.
        Assert.Same(knownFailure, unknownFailure);
        Assert.Same(knownFailure, neverIssuedFailure);
        Assert.Equal(OtpVerificationOutcome.Failed, knownFailure.Outcome);
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public async Task Correct_code_for_an_unknown_address_still_fails()
    {
        var harness = new OtpHarness(OtpHarness.Options());
        await harness.IssueAsync(OtpHarness.UnknownAddress);

        // The dispatcher captured a real code for the unknown address; even
        // presenting it must not authenticate a non-account.
        var result = await harness.VerifyAsync(harness.Dispatcher.LastCode, OtpHarness.UnknownAddress);

        Assert.Equal(OtpVerificationOutcome.Failed, result.Outcome);
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public async Task Unknown_addresses_accrue_lockout_exactly_like_known_ones()
    {
        var harness = new OtpHarness(OtpHarness.Options(maxVerificationFailures: 2));
        await harness.IssueAsync(OtpHarness.UnknownAddress);

        await harness.VerifyAsync("000000", OtpHarness.UnknownAddress);
        await harness.VerifyAsync("000000", OtpHarness.UnknownAddress);
        var locked = await harness.VerifyAsync("000000", OtpHarness.UnknownAddress);

        // Same failed-attempt counting and lockout path: an attacker cannot
        // infer account existence from whether lockout behavior appears.
        Assert.Equal(OtpVerificationOutcome.RateLimited, locked.Outcome);
    }
}
