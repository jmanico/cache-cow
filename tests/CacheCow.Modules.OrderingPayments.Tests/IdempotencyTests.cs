using System.Text;
using CacheCow.Modules.OrderingPayments.Idempotency;
using CacheCow.Modules.OrderingPayments.Orders;
using CacheCow.Modules.OrderingPayments.Submission;
using CacheCow.SharedKernel;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.OrderingPayments.Tests;

/// <summary>
/// Issue 037 (CC-ORD-005, CC-API-005, CC-SEC-015): idempotency keys are scoped
/// to the issuing tenant/account/guest session and bound to a request
/// fingerprint; replay returns the stored original, a mutated body conflicts
/// with 409 semantics, racing duplicates collapse to one execution, and
/// enforcement fails closed.
/// </summary>
public sealed class IdempotencyTests
{
    private static readonly IdempotencyScope TenantA = IdempotencyScope.ForPartnerTenant("partner-a");
    private static readonly IdempotencyScope TenantB = IdempotencyScope.ForPartnerTenant("partner-b");
    private static readonly IdempotencyKey Key = IdempotencyKey.Parse("key-0001");

    private static byte[] Content(string text) => Encoding.UTF8.GetBytes(text);

    private static (IdempotencyService Service, ManualTimeProvider Clock) NewService(TimeSpan? retention = null)
    {
        var clock = new ManualTimeProvider(Fixtures.T0);
        var store = new InMemoryIdempotencyStore(
            new IdempotencyOptions(retention ?? TimeSpan.FromHours(24)),
            clock);
        return (new IdempotencyService(store, new Sha256RequestFingerprintStrategy()), clock);
    }

    [Fact]
    [Requirement("CC-ORD-005")]
    [Requirement("CC-API-005")]
    public void Replay_with_same_scope_key_and_content_returns_the_original_without_reexecuting()
    {
        var (service, _) = NewService();
        var executions = 0;

        var first = service.Execute(TenantA, Key, Content("order-body"), () =>
        {
            executions++;
            return new object();
        });
        var second = service.Execute(TenantA, Key, Content("order-body"), () =>
        {
            executions++;
            return new object();
        });

        Assert.Equal(1, executions);
        Assert.False(first.WasReplay);
        Assert.True(second.WasReplay);
        Assert.Same(first.Value, second.Value);
    }

    [Fact]
    [Requirement("CC-ORD-005")]
    [Requirement("CC-QA-004")]
    public void Double_order_submission_with_same_key_returns_the_original_order_and_creates_no_duplicate()
    {
        var (service, _) = NewService();
        var submissionService = Fixtures.SubmissionService();
        var request = new OrderSubmissionRequest([new SubmittedCartLine(Fixtures.Brisket, 2)]);
        var buyer = BuyerIdentity.ForGuestSession("guest-session-9");
        var scope = IdempotencyScope.ForBuyer(buyer);
        var submissions = 0;

        Order SubmitOnce()
        {
            submissions++;
            return submissionService.Submit(request, buyer, Market.US);
        }

        var first = service.Execute(scope, Key, Content("brisket-x2"), SubmitOnce);
        var second = service.Execute(scope, Key, Content("brisket-x2"), SubmitOnce);

        Assert.Equal(1, submissions);
        Assert.Same(first.Value, second.Value);
        Assert.Equal(first.Value.Id, second.Value.Id);
        Assert.Equal(OrderState.Received, second.Value.State);
    }

    [Fact]
    [Requirement("CC-SEC-015")]
    public void Same_key_with_different_content_conflicts_never_replays_never_executes()
    {
        var (service, _) = NewService();
        var executions = 0;

        var original = service.Execute(TenantA, Key, Content("original"), () =>
        {
            executions++;
            return new object();
        });

        Assert.Throws<IdempotencyConflictException>(() =>
            service.Execute(TenantA, Key, Content("mutated"), () =>
            {
                executions++;
                return new object();
            }));

        Assert.Equal(1, executions);

        // The stored original is still intact and replayable with the
        // matching content.
        var replay = service.Execute(TenantA, Key, Content("original"), () =>
        {
            executions++;
            return new object();
        });
        Assert.True(replay.WasReplay);
        Assert.Same(original.Value, replay.Value);
        Assert.Equal(1, executions);
    }

    [Fact]
    [Requirement("CC-SEC-015")]
    [Requirement("CC-QA-005")]
    public void Keys_are_tenant_scoped_one_partners_key_never_collides_with_or_reads_anothers()
    {
        var (service, _) = NewService();
        var executions = 0;

        var partnerA = service.Execute(TenantA, Key, Content("same-body"), () =>
        {
            executions++;
            return new object();
        });
        var partnerB = service.Execute(TenantB, Key, Content("same-body"), () =>
        {
            executions++;
            return new object();
        });

        // Both executed in their own scope: B neither collided with A's entry
        // nor received A's stored result.
        Assert.Equal(2, executions);
        Assert.False(partnerB.WasReplay);
        Assert.NotSame(partnerA.Value, partnerB.Value);
    }

    [Fact]
    [Requirement("CC-SEC-015")]
    [Requirement("CC-ORD-001")]
    public void Guest_keys_are_scoped_to_the_guest_checkout_session()
    {
        var (service, _) = NewService();
        var sessionOne = IdempotencyScope.ForGuestSession("guest-session-1");
        var sessionTwo = IdempotencyScope.ForGuestSession("guest-session-2");
        var executions = 0;

        var one = service.Execute(sessionOne, Key, Content("cart"), () =>
        {
            executions++;
            return new object();
        });
        var two = service.Execute(sessionTwo, Key, Content("cart"), () =>
        {
            executions++;
            return new object();
        });

        Assert.Equal(2, executions);
        Assert.NotSame(one.Value, two.Value);

        // A tenant scope with the same identifier string is still a distinct
        // scope: the composite key includes the identity population.
        var tenantAlias = service.Execute(
            IdempotencyScope.ForPartnerTenant("guest-session-1"), Key, Content("cart"), () =>
            {
                executions++;
                return new object();
            });
        Assert.Equal(3, executions);
        Assert.NotSame(one.Value, tenantAlias.Value);
    }

    [Fact]
    [Requirement("CC-ORD-005")]
    [Requirement("CC-QA-004")]
    public async Task Racing_duplicates_with_the_same_key_produce_exactly_one_execution()
    {
        var (service, _) = NewService();
        var executions = 0;
        using var startTogether = new Barrier(8);

        var racers = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() =>
            {
                startTogether.SignalAndWait();
                return service.Execute(TenantA, Key, Content("race"), () =>
                {
                    Interlocked.Increment(ref executions);
                    return new object();
                });
            }))
            .ToArray();

        var results = await Task.WhenAll(racers);

        Assert.Equal(1, executions);
        Assert.All(results, r => Assert.Same(results[0].Value, r.Value));
        Assert.Equal(1, results.Count(r => !r.WasReplay));
    }

    [Fact]
    [Requirement("CC-ORD-005")]
    public void Failed_execution_releases_the_key_so_a_retry_is_not_poisoned()
    {
        // AC-07: idempotency protects side effects, not transient failures. An
        // operation that threw (no order created) must not pin the key to the
        // failure.
        var (service, _) = NewService();
        var attempts = 0;

        Assert.Throws<InvalidOperationException>(() =>
            service.Execute<object>(TenantA, Key, Content("body"), () =>
            {
                attempts++;
                throw new InvalidOperationException("downstream unavailable");
            }));

        var retried = service.Execute(TenantA, Key, Content("body"), () =>
        {
            attempts++;
            return new object();
        });

        Assert.Equal(2, attempts);
        Assert.False(retried.WasReplay);
    }

    [Fact]
    [Requirement("CC-API-005")]
    public void Replay_stops_after_the_retention_window_expires()
    {
        var (service, clock) = NewService(retention: TimeSpan.FromHours(1));
        var executions = 0;

        object Operation()
        {
            executions++;
            return new object();
        }

        service.Execute(TenantA, Key, Content("body"), Operation);

        clock.Advance(TimeSpan.FromMinutes(59));
        var withinWindow = service.Execute(TenantA, Key, Content("body"), Operation);
        Assert.True(withinWindow.WasReplay);

        clock.Advance(TimeSpan.FromMinutes(2));
        var afterWindow = service.Execute(TenantA, Key, Content("body"), Operation);
        Assert.False(afterWindow.WasReplay);
        Assert.Equal(2, executions);
    }

    [Fact]
    [Requirement("CC-API-005")]
    public void Retention_window_is_required_configuration_with_no_default()
    {
        // The window duration is an unratified open question (issue 037):
        // there is no parameterless construction and no baked-in value.
        Assert.DoesNotContain(
            typeof(IdempotencyOptions).GetConstructors(),
            c => c.GetParameters().Length == 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => new IdempotencyOptions(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new IdempotencyOptions(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    [Requirement("CC-ORD-005")]
    [Requirement("CC-SEC-015")]
    public void Unavailable_store_denies_processing_instead_of_running_unprotected()
    {
        var service = new IdempotencyService(new ThrowingIdempotencyStore(), new Sha256RequestFingerprintStrategy());
        var executions = 0;

        Assert.Throws<InvalidOperationException>(() =>
            service.Execute(TenantA, Key, Content("body"), () =>
            {
                executions++;
                return new object();
            }));

        Assert.Equal(0, executions);
    }

    [Fact]
    [Requirement("CC-SEC-015")]
    public void Default_fingerprint_is_sha256_over_the_supplied_request_bytes()
    {
        var strategy = new Sha256RequestFingerprintStrategy();

        var abc = strategy.ComputeFingerprint(Content("abc"));
        Assert.Equal(
            "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD",
            abc.Value);

        Assert.Equal(abc, strategy.ComputeFingerprint(Content("abc")));
        Assert.NotEqual(abc, strategy.ComputeFingerprint(Content("abd")));
    }

    [Fact]
    [Requirement("CC-API-005")]
    public void Idempotency_keys_are_validated_as_untrusted_input()
    {
        Assert.Throws<FormatException>(() => IdempotencyKey.Parse(""));
        Assert.Throws<FormatException>(() => IdempotencyKey.Parse("   "));
        Assert.Throws<FormatException>(() => IdempotencyKey.Parse("bad\nkey"));
        Assert.Throws<FormatException>(() => IdempotencyKey.Parse(new string('k', IdempotencyKey.MaxLength + 1)));

        Assert.Equal("key-0001", IdempotencyKey.Parse("key-0001").Value);
    }

    [Fact]
    [Requirement("CC-SEC-015")]
    public void Buyer_identity_maps_to_the_matching_scope_population()
    {
        var guestScope = IdempotencyScope.ForBuyer(BuyerIdentity.ForGuestSession("g-1"));
        Assert.Equal(IdempotencyScopeKind.GuestSession, guestScope.Kind);
        Assert.Equal("g-1", guestScope.Identifier);

        var accountScope = IdempotencyScope.ForBuyer(BuyerIdentity.ForAccount("a-1"));
        Assert.Equal(IdempotencyScopeKind.ConsumerAccount, accountScope.Kind);
        Assert.Equal("a-1", accountScope.Identifier);
    }
}
