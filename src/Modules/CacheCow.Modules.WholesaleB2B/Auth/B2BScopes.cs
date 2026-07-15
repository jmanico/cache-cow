using System.Collections.Frozen;

namespace CacheCow.Modules.WholesaleB2B.Auth;

/// <summary>
/// The complete, closed B2B scope model (CC-API-004): exactly these four
/// scopes exist. A token carrying any other scope value is rejected outright
/// by <see cref="B2BTokenClaimsValidator"/> (fail closed; SECURITY.md,
/// Authentication rule 8 — least privilege, RFC 9700).
/// </summary>
public static class B2BScopes
{
    public const string CatalogRead = "catalog:read";
    public const string OrdersWrite = "orders:write";
    public const string OrdersRead = "orders:read";
    public const string InvoicesRead = "invoices:read";

    /// <summary>Every scope that exists (CC-API-004: "API scopes are ...").</summary>
    public static FrozenSet<string> All { get; } =
        new[] { CatalogRead, OrdersWrite, OrdersRead, InvoicesRead }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// The read-only subset: the ceiling for bearer-only (not sender-constrained)
    /// tokens per CC-API-003 / SECURITY.md Authentication rule 6.
    /// </summary>
    public static FrozenSet<string> ReadOnly { get; } =
        new[] { CatalogRead, OrdersRead, InvoicesRead }.ToFrozenSet(StringComparer.Ordinal);
}
