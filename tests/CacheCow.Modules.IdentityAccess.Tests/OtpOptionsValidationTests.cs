using CacheCow.Modules.IdentityAccess.EmailOtp;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.IdentityAccess.Tests;

/// <summary>
/// Issue 059 (CC-SEC-016): configuration cannot weaken the spec-fixed bounds —
/// expiry is capped at 10 minutes, code length floors at 6 digits — and the
/// open-decision numbers (failure threshold, lockout, rate limits) are
/// required, positive values with no defaults.
/// </summary>
public sealed class OtpOptionsValidationTests
{
    private static EmailOtpOptions Build(
        int codeLength = 6,
        TimeSpan? codeLifetime = null,
        int maxVerificationFailures = 3,
        TimeSpan? lockoutDuration = null,
        TimeSpan? rateLimitWindow = null,
        int issuanceLimitPerAccount = 5,
        int issuanceLimitPerIp = 10,
        int verificationLimitPerAccount = 5,
        int verificationLimitPerIp = 10) =>
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

    [Fact]
    [Requirement("CC-SEC-016")]
    public void Lifetime_above_ten_minutes_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Build(codeLifetime: TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Build(codeLifetime: TimeSpan.FromHours(1)));
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public void Lifetime_of_exactly_ten_minutes_or_less_is_accepted()
    {
        Assert.Equal(TimeSpan.FromMinutes(10), Build(codeLifetime: TimeSpan.FromMinutes(10)).CodeLifetime);
        Assert.Equal(TimeSpan.FromMinutes(5), Build(codeLifetime: TimeSpan.FromMinutes(5)).CodeLifetime);
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public void Non_positive_lifetime_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Build(codeLifetime: TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => Build(codeLifetime: TimeSpan.FromSeconds(-1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(33)]
    [Requirement("CC-SEC-016")]
    public void Code_length_outside_the_allowed_range_is_rejected(int length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Build(codeLength: length));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(32)]
    [Requirement("CC-SEC-016")]
    public void Code_length_of_six_or_more_is_accepted(int length)
    {
        Assert.Equal(length, Build(codeLength: length).CodeLength);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [Requirement("CC-SEC-016")]
    public void Failure_threshold_below_one_is_rejected(int maxFailures)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Build(maxVerificationFailures: maxFailures));
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public void Non_positive_lockout_and_window_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Build(lockoutDuration: TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => Build(rateLimitWindow: TimeSpan.Zero));
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public void Rate_limits_below_one_are_rejected_for_all_four_scopes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Build(issuanceLimitPerAccount: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Build(issuanceLimitPerIp: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Build(verificationLimitPerAccount: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Build(verificationLimitPerIp: 0));
    }
}
