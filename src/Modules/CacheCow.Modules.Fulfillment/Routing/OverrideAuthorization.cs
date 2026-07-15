namespace CacheCow.Modules.Fulfillment.Routing;

/// <summary>
/// Typed proof that an authenticated staff actor holds the cross-region
/// override permission. The dashboard/Back Office context performs the actual
/// RBAC check (CC-DSH-002; SECURITY.md, Authentication rules 1 and 8; issue
/// 080) and issues this proof at the seam — this context models the
/// requirement that the proof exists, not the RBAC itself. Without an instance
/// there is no API path to cross-region fulfillment (CC-FUL-001).
/// </summary>
public sealed record OverrideAuthorization
{
    private OverrideAuthorization(string actorId)
    {
        ActorId = actorId;
    }

    /// <summary>The staff actor the permission was verified for, set from server-side identity (SECURITY.md, Input validation rule 3).</summary>
    public string ActorId { get; }

    public static OverrideAuthorization IssuedTo(string actorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        return new OverrideAuthorization(actorId);
    }
}
