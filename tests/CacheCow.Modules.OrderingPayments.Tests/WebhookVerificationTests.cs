using System.Reflection;
using System.Text;
using CacheCow.Modules.OrderingPayments.Webhooks;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.OrderingPayments.Tests;

/// <summary>
/// Issue 041 (CC-ORD-009, CC-SEC-014): inbound processor callbacks are
/// verified — HMAC-SHA256 over the raw body, constant-time, before any
/// parsing — with timestamp/nonce replay bounds; every failure mode is a
/// typed, fail-closed rejection; a verified event is constructible only by
/// the verifier; and no browser-redirect input type exists in the module.
/// </summary>
[Requirement("CC-ORD-009")]
[Requirement("CC-SEC-014")]
public sealed class WebhookVerificationTests
{
    private const string Body = """{"type":"payment.succeeded","order":"o-1"}""";

    [Fact]
    public void Valid_signature_within_bounds_yields_verified_event_with_raw_body()
    {
        var verifier = Fixtures.Verifier();
        var delivery = Fixtures.SignedDelivery(Fixtures.StripeSecret, Body, Fixtures.T0, "evt_1");

        var result = verifier.Verify(delivery);

        Assert.True(result.IsVerified);
        Assert.Null(result.Rejection);
        var verified = Assert.IsType<VerifiedProcessorEvent>(result.Event);
        Assert.Equal(Fixtures.StripeProcessor, verified.ProcessorName);
        Assert.Equal("evt_1", verified.EventId);
        Assert.Equal(Fixtures.T0, verified.Timestamp);
        // The signature-covered raw bytes ride along unmodified for
        // post-verification parsing (issues 039/040).
        Assert.Equal(Encoding.UTF8.GetBytes(Body), verified.RawBody.ToArray());
    }

    [Fact]
    public void Tampered_body_is_rejected_as_invalid_signature()
    {
        var verifier = Fixtures.Verifier();
        var signed = Fixtures.SignedDelivery(Fixtures.StripeSecret, Body, Fixtures.T0, "evt_1");
        var tampered = new UnverifiedWebhookDelivery(
            signed.ProcessorName,
            Encoding.UTF8.GetBytes("""{"type":"payment.succeeded","order":"o-ATTACKER"}"""),
            signed.SignatureHex,
            signed.Timestamp,
            signed.EventId);

        var result = verifier.Verify(tampered);

        AssertRejected(result, WebhookRejectionReason.InvalidSignature);
    }

    [Fact]
    public void Signature_from_wrong_secret_is_rejected()
    {
        var verifier = Fixtures.Verifier(); // knows only the Stripe secret
        var delivery = Fixtures.SignedDelivery(Fixtures.RazorpaySecret, Body, Fixtures.T0, "evt_1");

        AssertRejected(verifier.Verify(delivery), WebhookRejectionReason.InvalidSignature);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_signature_is_rejected_before_anything_else(string? signature)
    {
        var verifier = Fixtures.Verifier();
        var delivery = new UnverifiedWebhookDelivery(
            Fixtures.StripeProcessor, Encoding.UTF8.GetBytes(Body), signature, Fixtures.T0, "evt_1");

        AssertRejected(verifier.Verify(delivery), WebhookRejectionReason.MissingSignature);
    }

    [Theory]
    [InlineData("not-hex-at-all")]
    [InlineData("ZZZZ")]
    [InlineData("DEADBEEF")] // valid hex, wrong length for HMAC-SHA256
    public void Malformed_signature_is_rejected(string signature)
    {
        var verifier = Fixtures.Verifier();
        var delivery = new UnverifiedWebhookDelivery(
            Fixtures.StripeProcessor, Encoding.UTF8.GetBytes(Body), signature, Fixtures.T0, "evt_1");

        AssertRejected(verifier.Verify(delivery), WebhookRejectionReason.MalformedSignature);
    }

    [Fact]
    public void Stale_timestamp_is_rejected_even_with_valid_signature()
    {
        // AC-03: replay outside the timestamp bound is rejected despite a
        // perfectly valid signature.
        var verifier = Fixtures.Verifier();
        var stale = Fixtures.T0 - Fixtures.MaxEventAge - TimeSpan.FromSeconds(1);

        var result = verifier.Verify(Fixtures.SignedDelivery(Fixtures.StripeSecret, Body, stale, "evt_1"));

        AssertRejected(result, WebhookRejectionReason.StaleTimestamp);
    }

    [Fact]
    public void Future_timestamp_beyond_bound_is_rejected()
    {
        var verifier = Fixtures.Verifier();
        var future = Fixtures.T0 + Fixtures.MaxEventAge + TimeSpan.FromSeconds(1);

        var result = verifier.Verify(Fixtures.SignedDelivery(Fixtures.StripeSecret, Body, future, "evt_1"));

        AssertRejected(result, WebhookRejectionReason.StaleTimestamp);
    }

    [Fact]
    public void Missing_timestamp_or_event_id_is_rejected()
    {
        var verifier = Fixtures.Verifier();
        var raw = Encoding.UTF8.GetBytes(Body);
        var signed = Fixtures.SignedDelivery(Fixtures.StripeSecret, Body, Fixtures.T0, "evt_1");

        AssertRejected(
            verifier.Verify(new UnverifiedWebhookDelivery(
                Fixtures.StripeProcessor, raw, signed.SignatureHex, timestamp: null, "evt_1")),
            WebhookRejectionReason.MissingTimestamp);

        AssertRejected(
            verifier.Verify(new UnverifiedWebhookDelivery(
                Fixtures.StripeProcessor, raw, signed.SignatureHex, Fixtures.T0, eventId: null)),
            WebhookRejectionReason.MissingEventId);
    }

    [Fact]
    public void Replayed_event_id_is_rejected_on_second_delivery()
    {
        var verifier = Fixtures.Verifier();
        var delivery = Fixtures.SignedDelivery(Fixtures.StripeSecret, Body, Fixtures.T0, "evt_replay");

        Assert.True(verifier.Verify(delivery).IsVerified);
        AssertRejected(verifier.Verify(delivery), WebhookRejectionReason.ReplayedEventId);
    }

    [Fact]
    public void Replay_scope_is_per_processor()
    {
        var secrets = new StubSigningSecretProvider()
            .With(Fixtures.StripeProcessor, Fixtures.StripeSecret)
            .With("razorpay", Fixtures.RazorpaySecret);
        var verifier = Fixtures.Verifier(secrets);

        Assert.True(verifier.Verify(
            Fixtures.SignedDelivery(Fixtures.StripeSecret, Body, Fixtures.T0, "evt_shared")).IsVerified);
        Assert.True(verifier.Verify(
            Fixtures.SignedDelivery(Fixtures.RazorpaySecret, Body, Fixtures.T0, "evt_shared", "razorpay")).IsVerified);
    }

    [Fact]
    public void Forged_delivery_cannot_poison_the_nonce_store()
    {
        // Nonce registration happens only after signature+timestamp pass: an
        // attacker sending an unsigned delivery with the victim's event id
        // must not block the authentic delivery.
        var verifier = Fixtures.Verifier();
        var forged = Fixtures.SignedDelivery(Fixtures.RazorpaySecret, Body, Fixtures.T0, "evt_target");
        AssertRejected(verifier.Verify(forged), WebhookRejectionReason.InvalidSignature);

        var authentic = Fixtures.SignedDelivery(Fixtures.StripeSecret, Body, Fixtures.T0, "evt_target");
        Assert.True(verifier.Verify(authentic).IsVerified);
    }

    [Fact]
    public void Unconfigured_processor_secret_fails_closed()
    {
        var verifier = Fixtures.Verifier(new StubSigningSecretProvider()); // no secrets at all

        var result = verifier.Verify(Fixtures.SignedDelivery(Fixtures.StripeSecret, Body, Fixtures.T0, "evt_1"));

        AssertRejected(result, WebhookRejectionReason.SigningSecretUnavailable);
    }

    [Fact]
    public void Secret_provider_failure_fails_closed_never_skips_verification()
    {
        // AC-08/AC-06: Key Vault unavailability is a denial.
        var verifier = Fixtures.Verifier(new ThrowingSigningSecretProvider());

        var result = verifier.Verify(Fixtures.SignedDelivery(Fixtures.StripeSecret, Body, Fixtures.T0, "evt_1"));

        AssertRejected(result, WebhookRejectionReason.SigningSecretUnavailable);
    }

    [Fact]
    public void Replay_store_failure_fails_closed()
    {
        var verifier = Fixtures.Verifier(replayStore: new ThrowingWebhookReplayStore());

        var result = verifier.Verify(Fixtures.SignedDelivery(Fixtures.StripeSecret, Body, Fixtures.T0, "evt_1"));

        AssertRejected(result, WebhookRejectionReason.VerificationError);
    }

    [Fact]
    public void Rotation_previous_secret_still_verifies_while_listed()
    {
        // AC-06: rotation without downtime — any currently acceptable secret
        // verifies; a delisted secret stops verifying.
        var during = new StubSigningSecretProvider()
            .With(Fixtures.StripeProcessor, Fixtures.RotatedStripeSecret, Fixtures.StripeSecret);
        Assert.True(Fixtures.Verifier(during)
            .Verify(Fixtures.SignedDelivery(Fixtures.StripeSecret, Body, Fixtures.T0, "evt_old-secret"))
            .IsVerified);

        var after = new StubSigningSecretProvider()
            .With(Fixtures.StripeProcessor, Fixtures.RotatedStripeSecret);
        AssertRejected(
            Fixtures.Verifier(after)
                .Verify(Fixtures.SignedDelivery(Fixtures.StripeSecret, Body, Fixtures.T0, "evt_old-secret-2")),
            WebhookRejectionReason.InvalidSignature);
    }

    [Fact]
    public void Rejections_never_carry_body_signature_or_secret_material()
    {
        var verifier = Fixtures.Verifier();
        var delivery = Fixtures.SignedDelivery(Fixtures.RazorpaySecret, Body, Fixtures.T0, "evt_1");

        var rejection = verifier.Verify(delivery).Rejection;

        Assert.NotNull(rejection);
        var formatted = rejection.ToString();
        Assert.DoesNotContain(delivery.SignatureHex!, formatted, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("payment.succeeded", formatted, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", formatted, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Verified_event_is_constructible_only_by_the_verifier()
    {
        // Unverified payloads are unrepresentable downstream: no public
        // constructor and no public factory exists for VerifiedProcessorEvent.
        Assert.Empty(typeof(VerifiedProcessorEvent).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.DoesNotContain(
            typeof(VerifiedProcessorEvent).GetMethods(BindingFlags.Public | BindingFlags.Static),
            m => m.ReturnType == typeof(VerifiedProcessorEvent));

        // Nor can host code fabricate a "verified" result.
        Assert.Empty(typeof(WebhookVerificationResult).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void No_browser_redirect_input_type_exists_in_the_module()
    {
        // CC-ORD-009 AC-05, as a structural fact: there is no type modeling a
        // payment-redirect return, so "confirm on redirect" cannot be written.
        Assert.DoesNotContain(
            typeof(OrderingPaymentsModule).Assembly.GetTypes(),
            t => t.Name.Contains("Redirect", StringComparison.OrdinalIgnoreCase)
                || t.Name.Contains("ReturnUrl", StringComparison.OrdinalIgnoreCase)
                || t.Name.Contains("SuccessUrl", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Options_require_a_positive_max_event_age()
    {
        // The replay-window duration is an open decision (issue 041): there is
        // no default, and a non-positive window is rejected.
        Assert.Throws<ArgumentOutOfRangeException>(() => new WebhookVerificationOptions(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WebhookVerificationOptions(TimeSpan.FromMinutes(-1)));
    }

    private static void AssertRejected(WebhookVerificationResult result, WebhookRejectionReason expected)
    {
        Assert.False(result.IsVerified);
        Assert.Null(result.Event);
        var rejection = Assert.IsType<WebhookRejection>(result.Rejection);
        Assert.Equal(expected, rejection.Reason);
    }
}
