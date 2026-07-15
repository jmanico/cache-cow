using CacheCow.Modules.Invoicing.Access;
using CacheCow.Modules.Invoicing.Invoices;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.Invoicing.Tests;

/// <summary>
/// Issue 048, AC-03–AC-05: guest access requires a valid capability token
/// bound to exactly this invoice's order (CC-ORD-010; SECURITY.md,
/// Authentication rule 14); account access requires object-level ownership
/// (rule 9); everything else — including faults — is a uniform denial.
/// </summary>
public sealed class InvoiceAccessAuthorizationTests
{
    private const string ValidToken = "tok_4fz8Qk1mN7xW2cVb9sLpD3aH";

    /// <summary>
    /// Contract double for the issue-042 port: the real token lifecycle
    /// (entropy, expiry, revocation) lives in Ordering &amp; Payments; this
    /// double only maps a presented value to a verdict.
    /// </summary>
    private sealed class StubTokenValidator : IGuestOrderCapabilityTokenValidator
    {
        private readonly Dictionary<string, OrderReference> _valid = new(StringComparer.Ordinal);

        public bool ThrowOnValidate { get; set; }

        public void Accept(string token, OrderReference order) => _valid[token] = order;

        public GuestCapabilityTokenValidation Validate(string presentedToken)
        {
            if (ThrowOnValidate)
            {
                throw new InvalidOperationException("validator fault");
            }

            return _valid.TryGetValue(presentedToken, out var order)
                ? GuestCapabilityTokenValidation.ValidFor(order)
                : GuestCapabilityTokenValidation.Invalid();
        }
    }

    private static Invoice GuestInvoice() =>
        InvoiceFixtures.NewIssuer().Issue(InvoiceFixtures.Draft(Market.DE));

    private static Invoice AccountInvoice(string accountId) =>
        InvoiceFixtures.NewIssuer().Issue(
            InvoiceFixtures.Draft(Market.DE, customerAccount: AccountReference.Parse(accountId)));

    [Fact]
    [Requirement("CC-ORD-010")]
    [Requirement("CC-SEC-017")]
    public void Guest_access_is_granted_only_for_a_valid_token_bound_to_this_order()
    {
        var invoice = GuestInvoice();
        var validator = new StubTokenValidator();
        validator.Accept(ValidToken, invoice.Order);
        var authorizer = new GuestCapabilityTokenInvoiceAuthorizer(validator);

        var decision = authorizer.Authorize(invoice, new GuestTokenAccessRequest(ValidToken));

        Assert.True(decision.IsGranted);
    }

    [Theory]
    [Requirement("CC-ORD-010")]
    [Requirement("CC-SEC-017")]
    [InlineData("")] // missing
    [InlineData("tok_expired_or_revoked_or_unknown_value")] // expired/revoked/unknown are indistinguishable: all invalid
    public void Guest_access_is_denied_for_missing_expired_revoked_or_unknown_tokens(string presented)
    {
        var invoice = GuestInvoice();
        var validator = new StubTokenValidator(); // accepts nothing
        var authorizer = new GuestCapabilityTokenInvoiceAuthorizer(validator);

        var decision = authorizer.Authorize(invoice, new GuestTokenAccessRequest(presented));

        Assert.False(decision.IsGranted);
        Assert.Equal(InvoiceAccessDenialReason.CapabilityTokenInvalid, decision.DenialReason);
    }

    [Fact]
    [Requirement("CC-ORD-010")]
    [Requirement("CC-SEC-017")]
    public void Guest_access_is_denied_when_the_token_is_bound_to_another_order()
    {
        var invoice = GuestInvoice();
        var validator = new StubTokenValidator();
        validator.Accept(ValidToken, OrderReference.Parse("some-other-order"));
        var authorizer = new GuestCapabilityTokenInvoiceAuthorizer(validator);

        var decision = authorizer.Authorize(invoice, new GuestTokenAccessRequest(ValidToken));

        Assert.False(decision.IsGranted);
        Assert.Equal(InvoiceAccessDenialReason.CapabilityTokenBoundToOtherOrder, decision.DenialReason);
    }

    [Fact]
    [Requirement("CC-SEC-017")]
    public void Guest_authorizer_fails_closed_on_validator_fault()
    {
        var invoice = GuestInvoice();
        var validator = new StubTokenValidator { ThrowOnValidate = true };
        var authorizer = new GuestCapabilityTokenInvoiceAuthorizer(validator);

        var decision = authorizer.Authorize(invoice, new GuestTokenAccessRequest(ValidToken));

        // SECURITY.md, Logging rule 2: any exception in an authorization path
        // is a denial, never a bypass.
        Assert.False(decision.IsGranted);
        Assert.Equal(InvoiceAccessDenialReason.EvaluationFault, decision.DenialReason);
    }

    [Fact]
    [Requirement("CC-INV-002")]
    public void Account_owner_is_granted_object_level_access()
    {
        var invoice = AccountInvoice("acct-owner");
        var authorizer = new AccountSessionInvoiceAuthorizer();

        var decision = authorizer.Authorize(
            invoice, new AccountSessionAccessRequest(AccountReference.Parse("acct-owner")));

        Assert.True(decision.IsGranted);
    }

    [Fact]
    [Requirement("CC-INV-002")]
    public void Another_account_is_denied_cross_tenant_access()
    {
        var invoice = AccountInvoice("acct-owner");
        var authorizer = new AccountSessionInvoiceAuthorizer();

        var decision = authorizer.Authorize(
            invoice, new AccountSessionAccessRequest(AccountReference.Parse("acct-attacker")));

        // IDOR coverage (CC-QA-005; SECURITY.md, Authentication rule 9):
        // uniform denial, mapping to 404 at the HTTP boundary.
        Assert.False(decision.IsGranted);
        Assert.Equal(InvoiceAccessDenialReason.NotResourceOwner, decision.DenialReason);
    }

    [Fact]
    [Requirement("CC-ORD-010")]
    [Requirement("CC-INV-002")]
    public void Guest_invoice_is_never_reachable_through_a_session()
    {
        var guestInvoice = GuestInvoice(); // no owning account exists
        var authorizer = new AccountSessionInvoiceAuthorizer();

        var decision = authorizer.Authorize(
            guestInvoice, new AccountSessionAccessRequest(AccountReference.Parse("acct-any")));

        Assert.False(decision.IsGranted);
    }

    [Fact]
    [Requirement("CC-SEC-017")]
    public void Each_authorizer_denies_the_other_kind_of_request()
    {
        var invoice = AccountInvoice("acct-owner");
        var guestAuthorizer = new GuestCapabilityTokenInvoiceAuthorizer(new StubTokenValidator());
        var accountAuthorizer = new AccountSessionInvoiceAuthorizer();

        var guestOnAccountPath = accountAuthorizer.Authorize(invoice, new GuestTokenAccessRequest(ValidToken));
        var accountOnGuestPath = guestAuthorizer.Authorize(
            invoice, new AccountSessionAccessRequest(AccountReference.Parse("acct-owner")));

        Assert.False(guestOnAccountPath.IsGranted);
        Assert.False(accountOnGuestPath.IsGranted);
        Assert.Equal(InvoiceAccessDenialReason.UnsupportedRequestKind, guestOnAccountPath.DenialReason);
        Assert.Equal(InvoiceAccessDenialReason.UnsupportedRequestKind, accountOnGuestPath.DenialReason);
    }

    [Fact]
    [Requirement("CC-ORD-010")]
    public void Order_number_plus_email_access_is_unrepresentable()
    {
        // CC-ORD-010 prohibits the order-number-plus-email mechanism. The
        // request hierarchy is closed (private protected base ctor): exactly
        // the two ratified kinds exist, so the prohibited mechanism cannot
        // even be expressed against the authorizer port.
        var requestKinds = typeof(InvoiceAccessRequest).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(InvoiceAccessRequest).IsAssignableFrom(type))
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["AccountSessionAccessRequest", "GuestTokenAccessRequest"], requestKinds);

        var baseConstructors = typeof(InvoiceAccessRequest)
            .GetConstructors(System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic);
        Assert.All(baseConstructors, ctor => Assert.False(
            ctor.IsPublic || ctor.IsFamily || ctor.IsFamilyOrAssembly,
            "InvoiceAccessRequest must not be extensible outside the module (CC-ORD-010)."));
    }

    [Fact]
    [Requirement("CC-SEC-017")]
    public void A_denial_always_carries_a_loggable_reason_and_grant_carries_none()
    {
        Assert.Throws<InvalidOperationException>(
            () => InvoiceAccessDecision.Denied(InvoiceAccessDenialReason.None));
        Assert.Equal(InvoiceAccessDenialReason.None, InvoiceAccessDecision.Granted().DenialReason);
    }
}
