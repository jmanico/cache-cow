using CacheCow.Modules.OrderingPayments.Orders;
using CacheCow.SharedKernel.Testing;
using Xunit;

namespace CacheCow.Modules.OrderingPayments.Tests;

/// <summary>
/// Issue 035 (CC-ORD-006): the state machine is exactly
/// received -> confirmed -> packed -> shipped -> delivered with cancelled and
/// refunded as terminal branches; branch legality is unratified configuration
/// that fails closed; every transition is audited through the append-only
/// sink and an audit failure denies the transition.
/// </summary>
public sealed class OrderStateMachineTests
{
    private static readonly OrderState[] AllStates = Enum.GetValues<OrderState>();

    private static readonly OrderState[] LinearStates =
    [
        OrderState.Received,
        OrderState.Confirmed,
        OrderState.Packed,
        OrderState.Shipped,
        OrderState.Delivered,
    ];

    private static readonly Dictionary<OrderState, OrderState> LinearForward = new()
    {
        [OrderState.Received] = OrderState.Confirmed,
        [OrderState.Confirmed] = OrderState.Packed,
        [OrderState.Packed] = OrderState.Shipped,
        [OrderState.Shipped] = OrderState.Delivered,
    };

    /// <summary>Test-supplied branch table — NOT a ratified decision, purely a fixture (issue 035, Open Questions).</summary>
    private static BranchTransitionTable TestTable() => new(
        cancellableFrom: [OrderState.Received, OrderState.Confirmed],
        refundableFrom: [OrderState.Delivered]);

    private static Order OrderIn(OrderState state, OrderStateMachine machine)
    {
        var order = Fixtures.NewReceivedOrder();

        if (state == OrderState.Cancelled)
        {
            return machine.Transition(order, OrderState.Cancelled, "test:setup");
        }

        foreach (var next in LinearStates.Skip(1))
        {
            if (order.State == state)
            {
                break;
            }

            order = machine.Transition(order, next, "test:setup");
        }

        return state == OrderState.Refunded
            ? machine.Transition(order, OrderState.Refunded, "test:setup")
            : order;
    }

    [Fact]
    [Requirement("CC-ORD-006")]
    public void Linear_path_is_exhaustively_enforced_without_branch_table()
    {
        // Without a ratified branch table, only the four forward-adjacent
        // transitions are legal from any reachable state; branches fail closed
        // with the not-ratified error and everything else is illegal.
        foreach (var from in LinearStates)
        {
            foreach (var to in AllStates)
            {
                var machine = new OrderStateMachine(new RecordingAuditSink());
                var order = OrderIn(from, machine);
                Assert.Equal(from, order.State);

                if (LinearForward.TryGetValue(from, out var next) && next == to)
                {
                    var transitioned = machine.Transition(order, to, "test:actor");
                    Assert.Equal(to, transitioned.State);
                }
                else if (to is OrderState.Cancelled or OrderState.Refunded)
                {
                    var denied = Assert.Throws<BranchTransitionsNotRatifiedException>(
                        () => machine.Transition(order, to, "test:actor"));
                    Assert.Equal(from, denied.FromState);
                    Assert.Equal(to, denied.ToState);
                    Assert.Equal(from, order.State);
                }
                else
                {
                    Assert.Throws<IllegalOrderTransitionException>(
                        () => machine.Transition(order, to, "test:actor"));
                    Assert.Equal(from, order.State);
                }
            }
        }
    }

    [Fact]
    [Requirement("CC-ORD-006")]
    public void With_supplied_table_full_matrix_behaves_exactly_per_table()
    {
        // Every (from, to) pair across all seven states: legal iff
        // forward-adjacent or explicitly allowed by the supplied table.
        var table = TestTable();
        var expectedLegal = new HashSet<(OrderState, OrderState)>
        {
            (OrderState.Received, OrderState.Confirmed),
            (OrderState.Confirmed, OrderState.Packed),
            (OrderState.Packed, OrderState.Shipped),
            (OrderState.Shipped, OrderState.Delivered),
            (OrderState.Received, OrderState.Cancelled),
            (OrderState.Confirmed, OrderState.Cancelled),
            (OrderState.Delivered, OrderState.Refunded),
        };

        foreach (var from in AllStates)
        {
            foreach (var to in AllStates)
            {
                var machine = new OrderStateMachine(new RecordingAuditSink(), table);
                var order = OrderIn(from, machine);
                Assert.Equal(from, order.State);

                if (expectedLegal.Contains((from, to)))
                {
                    Assert.Equal(to, machine.Transition(order, to, "test:actor").State);
                }
                else
                {
                    Assert.Throws<IllegalOrderTransitionException>(
                        () => machine.Transition(order, to, "test:actor"));
                    Assert.Equal(from, order.State);
                }
            }
        }
    }

    [Fact]
    [Requirement("CC-ORD-006")]
    public void Cancelled_and_refunded_are_terminal_even_with_a_table()
    {
        var machine = new OrderStateMachine(new RecordingAuditSink(), TestTable());

        foreach (var terminal in new[] { OrderState.Cancelled, OrderState.Refunded })
        {
            var order = OrderIn(terminal, machine);
            foreach (var to in AllStates)
            {
                Assert.Throws<IllegalOrderTransitionException>(
                    () => machine.Transition(order, to, "test:actor"));
            }
        }
    }

    [Fact]
    [Requirement("CC-ORD-006")]
    public void Every_successful_transition_appends_exactly_one_audit_event_with_full_fields()
    {
        var sink = new RecordingAuditSink();
        var clock = new ManualTimeProvider(Fixtures.T0);
        var machine = new OrderStateMachine(sink, TestTable(), clock);
        var order = Fixtures.NewReceivedOrder();

        foreach (var to in LinearStates.Skip(1))
        {
            clock.Advance(TimeSpan.FromMinutes(5));
            order = machine.Transition(order, to, "staff:ops-agent-7");
        }

        clock.Advance(TimeSpan.FromMinutes(5));
        machine.Transition(order, OrderState.Refunded, "staff:finance-2");

        Assert.Equal(5, sink.Events.Count);

        var expectedPairs = new (OrderState From, OrderState To)[]
        {
            (OrderState.Received, OrderState.Confirmed),
            (OrderState.Confirmed, OrderState.Packed),
            (OrderState.Packed, OrderState.Shipped),
            (OrderState.Shipped, OrderState.Delivered),
            (OrderState.Delivered, OrderState.Refunded),
        };

        for (var i = 0; i < expectedPairs.Length; i++)
        {
            var auditEvent = sink.Events[i];
            Assert.Equal(OrderStateMachine.TransitionAction, auditEvent.Action);
            Assert.Equal(order.Id, auditEvent.OrderId);
            Assert.Equal(expectedPairs[i].From, auditEvent.FromState);
            Assert.Equal(expectedPairs[i].To, auditEvent.ToState);
            Assert.Equal(Fixtures.T0 + TimeSpan.FromMinutes(5 * (i + 1)), auditEvent.Timestamp);
        }

        Assert.Equal("staff:ops-agent-7", sink.Events[0].Actor);
        Assert.Equal("staff:finance-2", sink.Events[4].Actor);
    }

    [Fact]
    [Requirement("CC-ORD-006")]
    public void Rejected_transitions_append_no_audit_event_and_do_not_mutate()
    {
        var sink = new RecordingAuditSink();
        var machine = new OrderStateMachine(sink, TestTable());
        var order = Fixtures.NewReceivedOrder();

        Assert.Throws<IllegalOrderTransitionException>(
            () => machine.Transition(order, OrderState.Shipped, "test:actor"));

        Assert.Empty(sink.Events);
        Assert.Equal(OrderState.Received, order.State);
    }

    [Fact]
    [Requirement("CC-ORD-006")]
    public void Audit_append_failure_denies_the_transition_fail_closed()
    {
        // AC-03: no transition may exist without its audit record. When the
        // sink throws, the transition never commits.
        var machine = new OrderStateMachine(new ThrowingAuditSink(), TestTable());
        var order = Fixtures.NewReceivedOrder();

        Assert.Throws<InvalidOperationException>(
            () => machine.Transition(order, OrderState.Confirmed, "test:actor"));
        Assert.Equal(OrderState.Received, order.State);
    }

    [Fact]
    [Requirement("CC-ORD-006")]
    public void Audit_event_shape_is_closed_no_freeform_payload_can_carry_secrets_or_pii()
    {
        // AC-07 (negative case): the event exposes exactly actor, action,
        // object, before/after, timestamp — no payload field exists for
        // credentials, tokens, PANs, or PII to ride along.
        var propertyNames = typeof(OrderAuditEvent)
            .GetProperties()
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        string[] expected = ["Action", "Actor", "FromState", "OrderId", "Timestamp", "ToState"];
        Assert.Equal(expected, propertyNames);
    }

    [Fact]
    [Requirement("CC-ORD-006")]
    public void Branch_table_rejects_terminal_states_as_sources()
    {
        Assert.Throws<ArgumentException>(() => new BranchTransitionTable(
            cancellableFrom: [OrderState.Cancelled],
            refundableFrom: []));
        Assert.Throws<ArgumentException>(() => new BranchTransitionTable(
            cancellableFrom: [],
            refundableFrom: [OrderState.Refunded]));
    }

    [Fact]
    [Requirement("CC-ORD-006")]
    public void Transition_requires_a_non_empty_actor()
    {
        var machine = new OrderStateMachine(new RecordingAuditSink());
        var order = Fixtures.NewReceivedOrder();

        Assert.ThrowsAny<ArgumentException>(
            () => machine.Transition(order, OrderState.Confirmed, "  "));
    }

    [Fact]
    [Requirement("CC-ORD-006")]
    public void Order_state_is_writable_only_through_the_state_machine_api()
    {
        // AC-06: no public construction or mutation path for order state
        // exists outside the module — the setter-free State property plus the
        // internal constructor/WithState are the whole surface.
        var stateProperty = typeof(Order).GetProperty(nameof(Order.State))!;
        Assert.False(stateProperty.CanWrite);
        Assert.Empty(typeof(Order).GetConstructors());
    }
}
