using CacheCow.Modules.WholesaleB2B.Partners;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.WholesaleB2B.Tests;

/// <summary>Deterministic, manually advanced clock (server-controlled time in tests without wall-clock flake).</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    internal ManualTimeProvider(DateTimeOffset start) => _utcNow = start;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    internal void Advance(TimeSpan by) => _utcNow += by;
}

/// <summary>Records every appended partner audit event, in order.</summary>
internal sealed class RecordingPartnerAuditSink : IPartnerAuditSink
{
    internal List<PartnerAuditEvent> Events { get; } = [];

    public void Append(PartnerAuditEvent auditEvent) => Events.Add(auditEvent);
}

/// <summary>Simulates an unavailable append-only audit store (issue 049, AC-02: audit failure denies).</summary>
internal sealed class ThrowingPartnerAuditSink : IPartnerAuditSink
{
    public void Append(PartnerAuditEvent auditEvent) =>
        throw new InvalidOperationException("audit store unavailable");
}

/// <summary>Builders for partner tenancy and onboarding fixtures.</summary>
internal static class Fixtures
{
    internal static readonly DateTimeOffset T0 = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    internal static readonly DashboardActorProof Staff =
        DashboardActorProof.ForAuthenticatedStaff("staff:approver-1", "admin");

    internal static BusinessIdentity DeIdentity() => BusinessIdentity.Create(Market.DE, "DE123456789");

    internal static BusinessIdentity InIdentity() => BusinessIdentity.Create(Market.IN, "27AAPFU0939F1ZV");

    internal static BusinessIdentity UsIdentity() => BusinessIdentity.Create(Market.US, "12-3456789");

    internal static BusinessIdentity JpIdentity() => BusinessIdentity.Create(Market.JP, "T1234567890123");

    /// <summary>A fresh tenancy record; always starts in Draft (the only construction path).</summary>
    internal static PartnerTenant NewDraftTenant(
        string id = "partner-a",
        params BusinessIdentity[] identities) =>
        PartnerTenant.Create(
            PartnerId.Parse(id),
            "Kaltlager Feinkost GmbH",
            identities.Length > 0 ? identities : [DeIdentity()]);

    /// <summary>Drives a fresh tenant into the requested state through the workflow only.</summary>
    internal static PartnerTenant TenantIn(
        PartnerOnboardingState state,
        PartnerOnboardingWorkflow workflow,
        string id = "partner-a",
        params BusinessIdentity[] identities)
    {
        var tenant = NewDraftTenant(id, identities);
        if (state == PartnerOnboardingState.Draft)
        {
            return tenant;
        }

        tenant = workflow.Submit(tenant, Staff);
        return state switch
        {
            PartnerOnboardingState.Submitted => tenant,
            PartnerOnboardingState.Rejected => workflow.Reject(tenant, Staff),
            PartnerOnboardingState.Approved => workflow.Approve(tenant, Staff),
            PartnerOnboardingState.Suspended => workflow.Suspend(workflow.Approve(tenant, Staff), Staff),
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };
    }

    /// <summary>An approved tenant plus its wholesale access context.</summary>
    internal static PartnerTenantContext ApprovedContext(
        string id = "partner-a",
        params BusinessIdentity[] identities)
    {
        var workflow = new PartnerOnboardingWorkflow(new RecordingPartnerAuditSink());
        var approved = TenantIn(PartnerOnboardingState.Approved, workflow, id, identities);
        return PartnerTenantContext.ForApprovedTenant(approved);
    }
}
