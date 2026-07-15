using System.Text.RegularExpressions;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.OrderingPayments.Addresses;

/// <summary>
/// One field's validation rule inside a <see cref="MarketAddressSchema"/>:
/// required/optional, an inclusive length bound on attacker-controlled input,
/// and an optional anchored format pattern (postal codes). Rules are data —
/// the per-market differences live entirely in these declarations, never in
/// scattered conditionals (issue 038; CC-MKT-006 analog for address gating).
/// </summary>
public sealed record AddressFieldRule(string Field, bool Required, int MaxLength, Regex? Pattern);

/// <summary>
/// The explicit, data-driven address schema for one market (CC-ORD-002;
/// SECURITY.md, Input validation rule 1; schemas as single source of truth,
/// ARCHITECTURE.md Dependency rule 7). Construction is internal: the launch
/// schemas in <see cref="LaunchMarketAddressSchemas"/> are the module-owned
/// rule set, and the unresolved per-market rule details are open questions
/// (issue 038) — hosts select which schemas are active but do not author new
/// ones here.
/// </summary>
public sealed class MarketAddressSchema
{
    private readonly Func<ValidatedAddressFields, DeliveryAddress> _materializer;

    internal MarketAddressSchema(
        Market market,
        IReadOnlyList<AddressFieldRule> fields,
        Func<ValidatedAddressFields, DeliveryAddress> materializer)
    {
        Market = market;
        Fields = fields;
        _materializer = materializer;
    }

    public Market Market { get; }

    /// <summary>The complete declared field set; submitted fields outside it are unknown and rejected.</summary>
    public IReadOnlyList<AddressFieldRule> Fields { get; }

    /// <summary>Builds the typed address from fields that already passed every rule (validator-only path).</summary>
    internal DeliveryAddress Materialize(ValidatedAddressFields fields) => _materializer(fields);
}

/// <summary>Accessor over the validated field values handed to a schema's materializer.</summary>
internal sealed class ValidatedAddressFields
{
    private readonly IReadOnlyDictionary<string, string> _values;

    internal ValidatedAddressFields(IReadOnlyDictionary<string, string> values) => _values = values;

    internal string Required(string field) => _values[field];

    internal string? Optional(string field) => _values.TryGetValue(field, out var value) ? value : null;
}
