using CacheCow.Modules.OrderingPayments.Idempotency;
using CacheCow.Modules.OrderingPayments.Orders;
using CacheCow.Modules.OrderingPayments.Submission;
using CacheCow.SharedKernel.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CacheCow.Modules.OrderingPayments.Tests;

/// <summary>
/// Module wiring: the bounded context registers its services against its own
/// ports; host-supplied adapters and required configuration complete the
/// graph, and the state machine resolves without a branch table (failing
/// branches closed) until the open decision is ratified.
/// </summary>
public sealed class ModuleRegistrationTests
{
    private static ServiceProvider BuildHostLikeProvider()
    {
        var services = new ServiceCollection();

        // Host-supplied adapters and required configuration (no defaults exist
        // for the options; values here are test fixtures, not decisions).
        services.AddSingleton<ICanonicalPriceSource>(Fixtures.UsPrices());
        services.AddSingleton<IPromotionEvaluator>(StubPromotionEvaluator.None);
        services.AddSingleton<ITaxCalculator>(StubTaxCalculator.Zero);
        services.AddSingleton<IAuditSink>(new RecordingAuditSink());
        services.AddSingleton(new OrderSubmissionOptions(maxQuantityPerLine: 100));
        services.AddSingleton(new IdempotencyOptions(TimeSpan.FromHours(24)));

        services.AddOrderingPaymentsModule();
        return services.BuildServiceProvider();
    }

    [Fact]
    [Requirement("CC-ORD-006")]
    [Requirement("CC-ORD-005")]
    public void Module_registers_its_services_against_host_supplied_ports()
    {
        using var provider = BuildHostLikeProvider();

        Assert.NotNull(provider.GetRequiredService<OrderSubmissionService>());
        Assert.NotNull(provider.GetRequiredService<IdempotencyService>());
        Assert.NotNull(provider.GetRequiredService<OrderStateMachine>());
        Assert.IsType<Sha256RequestFingerprintStrategy>(provider.GetRequiredService<IRequestFingerprintStrategy>());
        Assert.IsType<InMemoryIdempotencyStore>(provider.GetRequiredService<IIdempotencyStore>());
    }

    [Fact]
    [Requirement("CC-ORD-006")]
    public void Resolved_state_machine_fails_branches_closed_until_a_table_is_ratified()
    {
        using var provider = BuildHostLikeProvider();
        var machine = provider.GetRequiredService<OrderStateMachine>();
        var order = Fixtures.NewReceivedOrder();

        // No BranchTransitionTable was registered — the open decision stands,
        // so cancellation is denied, while the fixed linear path works.
        Assert.Throws<BranchTransitionsNotRatifiedException>(
            () => machine.Transition(order, OrderState.Cancelled, "test:actor"));
        Assert.Equal(OrderState.Confirmed, machine.Transition(order, OrderState.Confirmed, "test:actor").State);
    }
}
