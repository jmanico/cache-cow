using CacheCow.Modules.BackOffice.Auditing;
using CacheCow.Modules.BackOffice.Dashboard;
using CacheCow.Modules.BackOffice.Inventory;
using CacheCow.Modules.BackOffice.Orders;
using CacheCow.Modules.BackOffice.Partners;
using CacheCow.Modules.BackOffice.Rbac;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.BackOffice.Tests;

/// <summary>
/// The TEST role–permission matrix for the issue 082/084/085 suites. This is a
/// TEST FIXTURE ONLY: the production matrix content is unauthored and needs a
/// human decision (issue 080, Open Questions; issues 082/084/085 each record
/// the same gap). Nothing here asserts which role SHOULD hold which
/// permission — the suites assert only that whatever the matrix says is
/// enforced, and that absence of a grant denies.
///
/// The grants are deliberately narrow and deliberately NOT a hierarchy: admin
/// does not hold order or inventory permissions, so "admin can do anything"
/// cannot accidentally become true (SECURITY.md, Authentication rule 8: no
/// implicit grants).
/// </summary>
internal static class DashboardTestMatrix
{
    internal static RolePermissionMatrix Create() =>
        RolePermissionMatrix.Create(new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
        {
            // Reads and transitions, but NO refunds: the refund-denial suites
            // need a role that legitimately manages orders yet cannot refund.
            ["ops-agent"] = ["orders.search", "orders.transition", "inventory.view"],

            // Refunds live with finance here; finance cannot transition.
            ["finance"] = ["orders.refund"],

            // Partner management and approval.
            ["admin"] = ["partners.manage", "partners.approve"],

            // A role with no dashboard-module grant at all: every module
            // endpoint must 404 for it.
            ["sales-viewer"] = ["analytics.view"],

            ["hr-admin"] = ["employees.manage"],
        });
}

/// <summary>Records appended audit events; can be switched to throw to prove append-before-effect.</summary>
internal sealed class RecordingAuditSink : IAuditEventSink
{
    private readonly List<AuditEvent> appended = [];

    /// <summary>Every event appended, in order.</summary>
    internal IReadOnlyList<AuditEvent> Appended => appended;

    /// <summary>When true, every append throws — the audit-store-unavailable fault.</summary>
    internal bool Throw { get; set; }

    /// <summary>When set, only appends whose action equals this value throw (to fail the ATTEMPT but not the outcome).</summary>
    internal string? ThrowForAction { get; set; }

    public void Append(AuditEvent auditEvent)
    {
        if (Throw || (ThrowForAction is { } action && string.Equals(auditEvent.Action, action, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Audit store unavailable (test fault injection).");
        }

        lock (appended)
        {
            appended.Add(auditEvent);
        }
    }
}

/// <summary>
/// Order-reader fake (the host adapts the Ordering &amp; Payments context in
/// production). Counts invocations so the suites can prove a port was never
/// touched.
/// </summary>
internal sealed class FakeOrderReader : IDashboardOrderReader
{
    private readonly Dictionary<string, DashboardOrderRow> byRef = new(StringComparer.Ordinal);

    internal bool Throw { get; set; }

    internal int SearchCalls { get; private set; }

    internal int FindCalls { get; private set; }

    internal DashboardOrderSearchQuery? LastQuery { get; private set; }

    internal void Add(DashboardOrderRow row) => byRef[row.OrderRef] = row;

    public DashboardPage<DashboardOrderRow> Search(DashboardOrderSearchQuery query)
    {
        SearchCalls++;
        LastQuery = query;
        if (Throw)
        {
            throw new InvalidOperationException("Order reader unavailable (test fault injection).");
        }

        var rows = byRef.Values
            .Where(row => query.Market is not { } market || row.Market == market)
            .Where(row => query.State is not { } state || row.State == state)
            .Where(row => query.OrderRef is not { } orderRef
                || string.Equals(row.OrderRef, orderRef, StringComparison.Ordinal))
            .OrderBy(row => row.OrderRef, StringComparer.Ordinal)
            .ToList();

        return DashboardPage.Create(rows, query.Page, query.PageSize, rows.Count);
    }

    public DashboardOrderRow? Find(string orderRef)
    {
        FindCalls++;
        if (Throw)
        {
            throw new InvalidOperationException("Order reader unavailable (test fault injection).");
        }

        return byRef.TryGetValue(orderRef, out var row) ? row : null;
    }
}

/// <summary>
/// Order-commands fake standing in for the Ordering &amp; Payments context.
///
/// It is NOT a state machine and deliberately holds no legality table: the
/// real CC-ORD-006 machine lives in that context (issue 035), and duplicating
/// it here would test the fake rather than the module. It simply returns the
/// scripted outcome, which is exactly what the Back Office must faithfully
/// relay.
/// </summary>
internal sealed class FakeOrderCommands : IDashboardOrderCommands
{
    /// <summary>Every transition invocation, in order — the capture that proves whether the port ran.</summary>
    internal List<(string OrderRef, DashboardOrderState Target, DashboardActorReference Actor)> Transitions { get; } = [];

    /// <summary>Every refund invocation, in order.</summary>
    internal List<(string OrderRef, DashboardActorReference Actor)> Refunds { get; } = [];

    /// <summary>Total port invocations — the assertion "the port was never invoked" reads this.</summary>
    internal int Invocations => Transitions.Count + Refunds.Count;

    internal bool Throw { get; set; }

    internal DashboardOrderCommandOutcome TransitionOutcome { get; set; } = DashboardOrderCommandOutcome.Applied;

    internal DashboardOrderCommandOutcome RefundOutcome { get; set; } = DashboardOrderCommandOutcome.Applied;

    internal Money RefundAmount { get; set; } = Money.FromMinorUnits(29_988, Currency.Eur);

    public DashboardOrderTransitionResult Transition(
        string orderRef, DashboardOrderState targetState, DashboardActorReference actor)
    {
        Transitions.Add((orderRef, targetState, actor));
        if (Throw)
        {
            throw new InvalidOperationException("Ordering context unavailable (test fault injection).");
        }

        return new DashboardOrderTransitionResult(TransitionOutcome, targetState);
    }

    public DashboardOrderRefundResult Refund(string orderRef, DashboardActorReference actor)
    {
        Refunds.Add((orderRef, actor));
        if (Throw)
        {
            throw new InvalidOperationException("Ordering context unavailable (test fault injection).");
        }

        return new DashboardOrderRefundResult(RefundOutcome, DashboardOrderState.Refunded, RefundAmount);
    }
}

/// <summary>Inventory-reader fake (the host adapts the Catalog &amp; Inventory context in production).</summary>
internal sealed class FakeInventoryReader : IDashboardInventoryReader
{
    private readonly List<DashboardInventoryRow> rows = [];

    internal bool Throw { get; set; }

    internal int SearchCalls { get; private set; }

    internal DashboardInventoryQuery? LastQuery { get; private set; }

    internal void Add(DashboardInventoryRow row) => rows.Add(row);

    public DashboardPage<DashboardInventoryRow> Search(DashboardInventoryQuery query)
    {
        SearchCalls++;
        LastQuery = query;
        if (Throw)
        {
            throw new InvalidOperationException("Inventory reader unavailable (test fault injection).");
        }

        var matching = rows
            .Where(row => query.ColdStoreId is not { } store
                || string.Equals(row.ColdStoreId, store, StringComparison.Ordinal))
            .Where(row => query.Market is not { } market || row.Market == market)
            .Where(row => query.Sku is not { } sku || row.Sku == sku)
            .ToList();

        return DashboardPage.Create(matching, query.Page, query.PageSize, matching.Count);
    }
}

/// <summary>Partner-directory fake (the host adapts the Wholesale &amp; B2B context in production).</summary>
internal sealed class FakePartnerDirectory : IDashboardPartnerDirectory
{
    private readonly Dictionary<string, DashboardPartnerDetail> byId = new(StringComparer.Ordinal);

    internal bool Throw { get; set; }

    internal int FindCalls { get; private set; }

    internal void Add(DashboardPartnerDetail detail) => byId[detail.Summary.PartnerId] = detail;

    public DashboardPage<DashboardPartnerRow> Search(DashboardPartnerQuery query)
    {
        if (Throw)
        {
            throw new InvalidOperationException("Partner directory unavailable (test fault injection).");
        }

        var rows = byId.Values
            .Select(detail => detail.Summary)
            .Where(row => query.State is not { } state || row.State == state)
            .OrderBy(row => row.PartnerId, StringComparer.Ordinal)
            .ToList();

        return DashboardPage.Create(rows, query.Page, query.PageSize, rows.Count);
    }

    public DashboardPartnerDetail? Find(string partnerId)
    {
        FindCalls++;
        if (Throw)
        {
            throw new InvalidOperationException("Partner directory unavailable (test fault injection).");
        }

        return byId.TryGetValue(partnerId, out var detail) ? detail : null;
    }
}

/// <summary>
/// Partner-workflow fake standing in for the Wholesale context's
/// PartnerOnboardingWorkflow. Like <see cref="FakeOrderCommands"/>, it holds
/// no workflow-legality table — that belongs to the owning context (issue
/// 049).
/// </summary>
internal sealed class FakePartnerWorkflow : IDashboardPartnerWorkflow
{
    /// <summary>Every invocation, in order — the capture proving whether the port ran.</summary>
    internal List<(string Action, string PartnerId, DashboardActorReference Actor)> Invocations { get; } = [];

    internal bool Throw { get; set; }

    internal DashboardPartnerCommandOutcome Outcome { get; set; } = DashboardPartnerCommandOutcome.Applied;

    public DashboardPartnerCommandResult Approve(string partnerId, DashboardActorReference actor) =>
        Record("approve", partnerId, actor, DashboardPartnerState.Approved);

    public DashboardPartnerCommandResult Reject(string partnerId, DashboardActorReference actor) =>
        Record("reject", partnerId, actor, DashboardPartnerState.Rejected);

    public DashboardPartnerCommandResult Suspend(string partnerId, DashboardActorReference actor) =>
        Record("suspend", partnerId, actor, DashboardPartnerState.Suspended);

    private DashboardPartnerCommandResult Record(
        string action, string partnerId, DashboardActorReference actor, DashboardPartnerState state)
    {
        Invocations.Add((action, partnerId, actor));
        if (Throw)
        {
            throw new InvalidOperationException("Wholesale context unavailable (test fault injection).");
        }

        return new DashboardPartnerCommandResult(Outcome, state);
    }
}

/// <summary>Fixtures for the dashboard suites.</summary>
internal static class DashboardTestData
{
    internal static readonly DateTimeOffset Now = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    internal const string OrderRef = "CC-ORD-1001";
    internal const string PartnerId = "partner-a";

    /// <summary>Staff whose step-up re-authentication is fresh (one minute ago).</summary>
    internal static StaffContext Staff(string role, string actorId = "staff-1") =>
        StaffContext.ForAuthenticatedStaff(actorId, role, Now.AddMinutes(-1));

    /// <summary>Staff who has never completed a step-up ceremony.</summary>
    internal static StaffContext StaffWithoutStepUp(string role, string actorId = "staff-1") =>
        StaffContext.ForAuthenticatedStaff(actorId, role, lastReauthenticatedAt: null);

    /// <summary>Staff whose step-up is older than the configured max age.</summary>
    internal static StaffContext StaffWithStaleStepUp(string role, string actorId = "staff-1") =>
        StaffContext.ForAuthenticatedStaff(actorId, role, Now.AddHours(-6));

    internal static DashboardOrderRow Order(
        string orderRef = OrderRef,
        DashboardOrderState state = DashboardOrderState.Shipped,
        Market? market = null,
        long totalMinorUnits = 29_988) =>
        DashboardOrderRow.Create(
            orderRef,
            market ?? Market.DE,
            state,
            Now.AddDays(-2),
            Money.FromMinorUnits(totalMinorUnits, Currency.Eur));

    internal static DashboardInventoryRow Inventory(
        string coldStoreId = "cold-store-de-1",
        string sku = "SKU-BRISKET",
        long quantity = 120,
        DashboardStockState state = DashboardStockState.InStock,
        int? serviceLevelBasisPoints = 9_950,
        Market? market = null) =>
        DashboardInventoryRow.Create(
            coldStoreId,
            market ?? Market.DE,
            SkuId.Parse(sku),
            quantity,
            state,
            serviceLevelBasisPoints);

    internal static DashboardPartnerDetail Partner(
        string partnerId = PartnerId,
        DashboardPartnerState state = DashboardPartnerState.Submitted,
        int netDays = 60) =>
        DashboardPartnerDetail.Create(
            DashboardPartnerRow.Create(partnerId, "Beispiel Grosshandel GmbH", state),
            [DashboardPartnerIdentity.Create(Market.DE, "USt-IdNr.", "DE123456789")],
            netDays);
}
