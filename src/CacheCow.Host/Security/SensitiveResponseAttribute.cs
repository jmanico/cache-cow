namespace CacheCow.Host.Security;

/// <summary>
/// Endpoint metadata marking a response as sensitive: the response is emitted
/// with Cache-Control: no-store and must never be edge-cached (SECURITY.md,
/// HTTP boundary rules 3 and 10; CC-SEC-003, CC-SEC-013/CC-MKT-009 header
/// half). Authenticated responses receive no-store automatically; use this
/// for sensitive-but-anonymous surfaces (cart, checkout, guest order status).
/// Attach via [SensitiveResponse] or .WithMetadata(new SensitiveResponseAttribute()).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class SensitiveResponseAttribute : Attribute
{
}
