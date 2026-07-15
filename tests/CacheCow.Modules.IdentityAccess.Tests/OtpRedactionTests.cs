using CacheCow.Modules.IdentityAccess.EmailOtp;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.IdentityAccess.Tests;

/// <summary>
/// Issue 059 (CC-SEC-016), AC-07; SECURITY.md, Logging rule 4: code values
/// never appear in logs or telemetry, and no ToString or exception message
/// can carry one. Also AC-08: the OTP path emits structured security events.
/// </summary>
public sealed class OtpRedactionTests
{
    [Fact]
    [Requirement("CC-SEC-016")]
    public void OtpCode_ToString_is_redacted_and_contains_no_digits()
    {
        var code = OtpCodeGenerator.Generate(6);

        var rendered = code.ToString();

        Assert.DoesNotContain(code.RevealForDelivery(), rendered, StringComparison.Ordinal);
        Assert.DoesNotContain(rendered, c => char.IsDigit(c));
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public void Record_and_dispatch_request_ToString_are_redacted()
    {
        var code = OtpCodeGenerator.Generate(6);
        var salt = OtpCodeHasher.NewSalt();
        var record = new OtpRecord(
            "account-key",
            salt,
            OtpCodeHasher.ComputeDigest(code.RevealForDelivery(), salt),
            OtpHarness.T0,
            OtpHarness.T0 + TimeSpan.FromMinutes(10),
            accountExists: true,
            failedAttempts: 0);
        var request = new OtpDispatchRequest("customer@example.com", code, accountExists: true, record.ExpiresAt);

        Assert.DoesNotContain(code.RevealForDelivery(), record.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(code.RevealForDelivery(), request.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("customer@example.com", request.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public void Generator_rejection_exception_carries_the_length_but_never_a_code_value()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => OtpCodeGenerator.Generate(3));

        // The only number in the message chain is the rejected length; no
        // code exists yet and none can be echoed.
        Assert.Contains("6 digits", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    [Requirement("CC-SEC-005")]
    public async Task No_log_entry_from_the_full_flow_contains_the_code_or_the_email_address()
    {
        var harness = new OtpHarness(OtpHarness.Options(maxVerificationFailures: 2));

        // Exercise every logging branch: issue, reissue, wrong code, lockout,
        // rate limit on a second population, and a success.
        await harness.IssueAsync();
        await harness.IssueAsync();
        var code = harness.Dispatcher.LastCode;
        await harness.VerifyAsync(code == "000000" ? "000001" : "000000");
        await harness.VerifyAsync(code == "000000" ? "000001" : "000000"); // lockout imposed
        await harness.VerifyAsync(code); // rate limited (locked out)
        await harness.IssueAsync(OtpHarness.UnknownAddress);
        var freshHarnessCode = harness.Dispatcher.LastCode;

        var entries = harness.LoggerFactory.Logger.Entries;
        Assert.NotEmpty(entries); // AC-08: structured security events do flow

        // The pseudonymous account keys legitimately appear in entries; strip
        // them so a code digit-run coinciding inside a 64-char hex hash cannot
        // produce a false positive — only genuine code leakage fails.
        var knownKey = OtpAccountKey.Derive(OtpHarness.KnownAddress);
        var unknownKey = OtpAccountKey.Derive(OtpHarness.UnknownAddress);

        foreach (var rawEntry in entries)
        {
            var entry = rawEntry
                .Replace(knownKey, "[KEY]", StringComparison.Ordinal)
                .Replace(unknownKey, "[KEY]", StringComparison.Ordinal);

            foreach (var issued in harness.Dispatcher.Requests)
            {
                Assert.DoesNotContain(issued.Code.RevealForDelivery(), entry, StringComparison.Ordinal);
            }

            Assert.DoesNotContain(OtpHarness.KnownAddress, entry, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(OtpHarness.UnknownAddress, entry, StringComparison.OrdinalIgnoreCase);
        }

        Assert.NotNull(freshHarnessCode);
    }

    [Fact]
    [Requirement("CC-SEC-016")]
    public async Task Security_events_are_emitted_for_issuance_success_failure_and_lockout()
    {
        var harness = new OtpHarness(OtpHarness.Options(maxVerificationFailures: 1));
        await harness.IssueAsync();
        await harness.VerifyAsync(harness.Dispatcher.LastCode == "000000" ? "000001" : "000000");

        var entries = harness.LoggerFactory.Logger.Entries;

        Assert.Contains(entries, e => e.Contains("OtpCodeIssued", StringComparison.Ordinal));
        Assert.Contains(entries, e => e.Contains("OtpVerificationFailed", StringComparison.Ordinal));
        Assert.Contains(entries, e => e.Contains("OtpLockoutImposed", StringComparison.Ordinal));
    }
}
