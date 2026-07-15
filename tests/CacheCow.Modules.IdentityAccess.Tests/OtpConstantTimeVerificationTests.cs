using CacheCow.Modules.IdentityAccess.EmailOtp;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.IdentityAccess.Tests;

/// <summary>
/// Issue 059 (CC-SEC-016), AC-05 — structural constant-time verification:
/// comparison is digest-then-FixedTimeEquals by API design (the hasher exposes
/// no direct string comparison), every input maps to a fixed 32-byte digest,
/// and mismatched-length submissions take the same digest path as well-formed
/// ones (no pre-comparison length or format branch).
/// </summary>
public sealed class OtpConstantTimeVerificationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("123456")]
    [InlineData("not-even-digits")]
    [InlineData("12345678901234567890123456789012345678901234567890123456789012345")]
    [Requirement("CC-SEC-016")]
    public void Every_input_maps_to_the_same_fixed_digest_length(string input)
    {
        var salt = OtpCodeHasher.NewSalt();

        var digest = OtpCodeHasher.ComputeDigest(input, salt);

        // Fixed-length digests are what make FixedTimeEquals length-independent:
        // the comparison never sees the submission's length or content shape.
        Assert.Equal(OtpCodeHasher.DigestSizeBytes, digest.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("123456789012345678901234567890")]
    [InlineData("no digits at all")]
    [Requirement("CC-SEC-016")]
    public void Mismatched_length_submissions_take_the_digest_path_and_simply_fail(string submission)
    {
        var salt = OtpCodeHasher.NewSalt();
        var stored = OtpCodeHasher.ComputeDigest("482913", salt);

        // No exception, no early exit: the wrong-length value is digested and
        // compared exactly like a well-formed code.
        Assert.False(OtpCodeHasher.Matches(submission, salt, stored));
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public void Correct_code_matches_and_any_other_code_does_not()
    {
        var salt = OtpCodeHasher.NewSalt();
        var stored = OtpCodeHasher.ComputeDigest("482913", salt);

        Assert.True(OtpCodeHasher.Matches("482913", salt, stored));
        Assert.False(OtpCodeHasher.Matches("482914", salt, stored));
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public void Digests_are_salted_so_equal_codes_never_share_stored_state()
    {
        var saltA = OtpCodeHasher.NewSalt();
        var saltB = OtpCodeHasher.NewSalt();

        var digestA = OtpCodeHasher.ComputeDigest("482913", saltA);
        var digestB = OtpCodeHasher.ComputeDigest("482913", saltB);

        Assert.False(digestA.AsSpan().SequenceEqual(digestB));
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public async Task Wrong_length_submissions_flow_through_the_normal_failure_path_end_to_end()
    {
        var harness = new OtpHarness(OtpHarness.Options(maxVerificationFailures: 2));
        await harness.IssueAsync();

        // Wrong-length submissions are counted like any other failure and
        // reach lockout — proof they were digested and compared, not rejected
        // by an early format branch ahead of the constant-time core.
        Assert.Equal(OtpVerificationOutcome.Failed, (await harness.VerifyAsync("1")).Outcome);
        Assert.Equal(OtpVerificationOutcome.Failed, (await harness.VerifyAsync("")).Outcome);
        Assert.Equal(OtpVerificationOutcome.RateLimited, (await harness.VerifyAsync("1")).Outcome);
    }
}
