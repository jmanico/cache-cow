namespace CacheCow.Modules.OrderingPayments.Addresses;

/// <summary>
/// The client-bound address payload: named string fields, nothing else
/// (issue 038; CC-ORD-002). Every value is attacker-controlled input crossing
/// the trust boundary (SECURITY.md, Input validation rule 1) and grants
/// nothing until <see cref="AddressValidator"/> validates it against the
/// transacting market's schema. Field names are matched exactly
/// (case-sensitive ordinal): anything the schema does not declare is rejected
/// as unknown, never ignored (Input validation rule 2). This type deliberately
/// has no ToString over its contents — raw submission values never ride into
/// logs (SECURITY.md, Logging rules 4–5).
/// </summary>
public sealed class AddressSubmission
{
    public AddressSubmission(IReadOnlyDictionary<string, string?> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        Fields = new Dictionary<string, string?>(fields, StringComparer.Ordinal);
    }

    /// <summary>Submitted field name/value pairs (defensive copy; ordinal keys).</summary>
    public IReadOnlyDictionary<string, string?> Fields { get; }
}
