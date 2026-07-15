using CacheCow.Modules.IdentityAccess.EmailOtp;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.IdentityAccess.Tests;

/// <summary>
/// Issue 059 (CC-SEC-016): codes come from the cryptographic RNG via
/// rejection sampling, honor the configured length (at least 6 digits),
/// contain only digits, and are uniformly distributed.
/// </summary>
public sealed class OtpCodeGenerationTests
{
    [Theory]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(10)]
    [Requirement("CC-SEC-016")]
    public void Generated_code_has_configured_length_and_only_ascii_digits(int length)
    {
        var code = OtpCodeGenerator.Generate(length);
        var digits = code.RevealForDelivery();

        Assert.Equal(length, code.Length);
        Assert.Equal(length, digits.Length);
        Assert.All(digits, c => Assert.True(char.IsAsciiDigit(c)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(5)]
    [Requirement("CC-SEC-016")]
    public void Lengths_below_the_six_digit_floor_are_rejected(int length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OtpCodeGenerator.Generate(length));
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public void Digit_distribution_is_roughly_uniform()
    {
        // 2000 codes x 6 digits = 12000 samples; expected 1200 per digit
        // (sd ~ 33). Bounds sit ~9 sigma out, so a correct generator cannot
        // realistically flake while a biased one (e.g. plain modulo over the
        // full byte range would skew digits 0-5 upward by 4/256 each) drifts.
        var counts = new int[10];

        for (var i = 0; i < 2000; i++)
        {
            foreach (var c in OtpCodeGenerator.Generate(6).RevealForDelivery())
            {
                counts[c - '0']++;
            }
        }

        Assert.All(counts, count => Assert.InRange(count, 900, 1500));
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public void Codes_do_not_repeat_across_generations()
    {
        var distinct = new HashSet<string>();

        for (var i = 0; i < 100; i++)
        {
            distinct.Add(OtpCodeGenerator.Generate(8).RevealForDelivery());
        }

        // 100 draws from a 10^8 space: collisions are astronomically unlikely;
        // a stuck or seeded generator collapses this set immediately.
        Assert.True(distinct.Count > 95, $"only {distinct.Count} distinct codes out of 100");
    }
}
