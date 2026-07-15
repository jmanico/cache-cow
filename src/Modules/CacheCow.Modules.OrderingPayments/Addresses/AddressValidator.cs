using CacheCow.SharedKernel;

namespace CacheCow.Modules.OrderingPayments.Addresses;

/// <summary>
/// Server-side, per-market address validation (issue 038; CC-ORD-002;
/// SECURITY.md, Input validation rule 1). The single validation path from an
/// untrusted <see cref="AddressSubmission"/> to a typed
/// <see cref="DeliveryAddress"/>: every rule lives in the market's
/// <see cref="MarketAddressSchema"/> data, selected by the server-resolved
/// transacting market (CC-SEC-012) — never by <c>Accept-Language</c>,
/// geolocation, or any client hint (SECURITY.md, Authentication rule 10).
///
/// Invalid input is rejected, never sanitized into acceptance: no trimming,
/// no coercion, no dropping of unknown or blank fields. A market with no
/// configured schema fails closed. Client-side form validation is UX only and
/// is never consulted here (issue 038, AC-05).
/// </summary>
public sealed class AddressValidator
{
    private readonly Dictionary<string, MarketAddressSchema> _schemas;

    /// <param name="schemas">The active per-market schemas (one per market); markets without a schema fail closed.</param>
    public AddressValidator(IEnumerable<MarketAddressSchema> schemas)
    {
        ArgumentNullException.ThrowIfNull(schemas);

        _schemas = new Dictionary<string, MarketAddressSchema>(StringComparer.Ordinal);
        foreach (var schema in schemas)
        {
            ArgumentNullException.ThrowIfNull(schema, nameof(schemas));
            if (!_schemas.TryAdd(schema.Market.Code, schema))
            {
                throw new ArgumentException(
                    $"Duplicate address schema for market '{schema.Market.Code}'.",
                    nameof(schemas));
            }
        }
    }

    /// <summary>
    /// Validates the submission against the transacting market's schema and
    /// returns the typed, structured address. Throws
    /// <see cref="AddressRejectedException"/> — listing every failed field
    /// generically — on any violation; nothing partial is ever produced.
    /// </summary>
    /// <param name="submission">Untrusted client-bound field payload.</param>
    /// <param name="transactingMarket">Server-resolved transacting market (CC-SEC-012), never a client hint.</param>
    public DeliveryAddress Validate(AddressSubmission submission, Market transactingMarket)
    {
        ArgumentNullException.ThrowIfNull(submission);

        if (!_schemas.TryGetValue(transactingMarket.Code, out var schema))
        {
            // Fail closed: no schema means no validation basis, not a pass
            // (SECURITY.md, Logging rule 2).
            throw new AddressRejectedException(
                transactingMarket.Code,
                [new AddressFieldError("(market)", AddressRejectionCode.MarketNotConfigured)]);
        }

        var errors = new List<AddressFieldError>();

        var declared = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in schema.Fields)
        {
            declared.Add(rule.Field);
        }

        foreach (var submitted in submission.Fields.Keys)
        {
            if (!declared.Contains(submitted))
            {
                errors.Add(new AddressFieldError(submitted, AddressRejectionCode.UnknownField));
            }
        }

        var validated = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rule in schema.Fields)
        {
            var present = submission.Fields.TryGetValue(rule.Field, out var value);

            if (!present || string.IsNullOrWhiteSpace(value))
            {
                if (rule.Required)
                {
                    errors.Add(new AddressFieldError(rule.Field, AddressRejectionCode.MissingRequiredField));
                }
                else if (present)
                {
                    // A blank optional field is rejected, not silently dropped
                    // (reject-not-sanitize, SECURITY.md, Input validation rule 1).
                    errors.Add(new AddressFieldError(rule.Field, AddressRejectionCode.BlankValue));
                }

                continue;
            }

            if (value.Any(char.IsControl))
            {
                errors.Add(new AddressFieldError(rule.Field, AddressRejectionCode.InvalidCharacters));
                continue;
            }

            if (value.Length > rule.MaxLength)
            {
                errors.Add(new AddressFieldError(rule.Field, AddressRejectionCode.ValueTooLong));
                continue;
            }

            if (rule.Pattern is { } pattern && !pattern.IsMatch(value))
            {
                errors.Add(new AddressFieldError(rule.Field, AddressRejectionCode.InvalidFormat));
                continue;
            }

            validated[rule.Field] = value;
        }

        if (errors.Count > 0)
        {
            throw new AddressRejectedException(transactingMarket.Code, errors);
        }

        return schema.Materialize(new ValidatedAddressFields(validated));
    }
}
