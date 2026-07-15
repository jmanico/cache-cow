using CacheCow.Modules.ContentLocalization.Resources.MessageFormat;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.ContentLocalization.Resources;

/// <summary>The validation rule a translation resource violated (CC-I18N-002; CC-QA-006).</summary>
public enum TranslationViolationRule
{
    /// <summary>A launch locale (CC-I18N-001) has no resources at all.</summary>
    MissingLocale,

    /// <summary>Resources exist for a locale outside the launch set — rejected, not accepted provisionally.</summary>
    UnexpectedLocale,

    /// <summary>Key parity: a key present in at least one locale is missing in this one.</summary>
    MissingKey,

    /// <summary>HTML markup ('&lt;') inside a string resource (SECURITY.md, Input validation rule 7).</summary>
    HtmlInResource,

    /// <summary>Not valid ICU MessageFormat within the supported subset.</summary>
    MalformedMessage,

    /// <summary>Placeholder set (names and kinds) differs from the reference locale for the same key.</summary>
    PlaceholderMismatch,
}

/// <summary>
/// One violation, naming key, locale, and rule. The raw invalid resource
/// content is deliberately not echoed (SECURITY.md, Logging rule 5).
/// </summary>
public sealed record TranslationViolation(
    TranslationViolationRule Rule,
    string? Key,
    string? LocaleTag,
    string Detail);

/// <summary>The outcome of validating a full resource set. Invalid means reject — no warn-only mode.</summary>
public sealed class TranslationValidationResult
{
    internal TranslationValidationResult(IReadOnlyList<TranslationViolation> violations)
    {
        Violations = violations;
    }

    public bool IsValid => Violations.Count == 0;

    public IReadOnlyList<TranslationViolation> Violations { get; }
}

/// <summary>
/// The CI-runnable validation service for translation resources (CC-I18N-002;
/// CC-QA-006): every locale file is untrusted input ("five languages means
/// five supply chains for strings" — SECURITY.md, Input validation rule 7).
/// Validates: exact launch-locale coverage, key parity across all seven
/// locales, no HTML in any string value, ICU MessageFormat well-formedness
/// (subset), and placeholder parity (names and kinds) across locales. Invalid
/// input is rejected, never sanitized into acceptance (rule 1).
/// </summary>
public static class TranslationResourceValidator
{
    /// <summary>The reference locale for placeholder parity (issue 064 open question notes the specs do not name one; en-US is used).</summary>
    private static readonly Locale ReferenceLocale = Locale.Parse("en-US");

    public static TranslationValidationResult Validate(TranslationResourceSet set)
    {
        ArgumentNullException.ThrowIfNull(set);

        var violations = new List<TranslationViolation>();
        var resources = set.Resources;

        foreach (var locale in LaunchLocales.All)
        {
            if (!resources.ContainsKey(locale))
            {
                violations.Add(new TranslationViolation(
                    TranslationViolationRule.MissingLocale,
                    Key: null,
                    LocaleTag: locale.Tag,
                    $"No resources for launch locale '{locale.Tag}' (CC-I18N-001/002)."));
            }
        }

        foreach (var locale in resources.Keys)
        {
            if (!LaunchLocales.Contains(locale))
            {
                violations.Add(new TranslationViolation(
                    TranslationViolationRule.UnexpectedLocale,
                    Key: null,
                    LocaleTag: locale.Tag,
                    $"'{locale.Tag}' is not a launch locale (CC-I18N-001); rejected rather than accepted provisionally."));
            }
        }

        // Key parity over the union of keys: a key present anywhere must be
        // present everywhere (CC-I18N-002).
        var allKeys = resources.Values
            .SelectMany(keys => keys.Keys)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var parsed = new Dictionary<(Locale Locale, string Key), IcuMessage>();

        foreach (var locale in LaunchLocales.All)
        {
            if (!resources.TryGetValue(locale, out var keys))
            {
                continue;
            }

            foreach (var key in allKeys)
            {
                if (!keys.TryGetValue(key, out var value))
                {
                    violations.Add(new TranslationViolation(
                        TranslationViolationRule.MissingKey,
                        key,
                        locale.Tag,
                        $"Key '{key}' is missing in locale '{locale.Tag}' (key parity, CC-I18N-002)."));
                    continue;
                }

                if (value.Contains('<', StringComparison.Ordinal))
                {
                    violations.Add(new TranslationViolation(
                        TranslationViolationRule.HtmlInResource,
                        key,
                        locale.Tag,
                        $"Key '{key}' in locale '{locale.Tag}' contains '<'; HTML is not permitted in string resources (SECURITY.md, Input validation rule 7)."));
                    continue;
                }

                if (!IcuMessageParser.TryParse(value, out var message, out var error))
                {
                    violations.Add(new TranslationViolation(
                        TranslationViolationRule.MalformedMessage,
                        key,
                        locale.Tag,
                        $"Key '{key}' in locale '{locale.Tag}' is not valid ICU MessageFormat: {error}"));
                    continue;
                }

                parsed[(locale, key)] = message;
            }
        }

        // Placeholder parity against the reference locale (name + kind),
        // including placeholders nested in plural/select branches.
        foreach (var key in allKeys)
        {
            if (!parsed.TryGetValue((ReferenceLocale, key), out var reference))
            {
                continue; // missing or malformed in the reference locale is already reported
            }

            var expected = reference.Placeholders.ToHashSet();

            foreach (var locale in LaunchLocales.All)
            {
                if (locale == ReferenceLocale || !parsed.TryGetValue((locale, key), out var candidate))
                {
                    continue;
                }

                if (!expected.SetEquals(candidate.Placeholders))
                {
                    var expectedNames = string.Join(", ", expected.Select(Describe).Order(StringComparer.Ordinal));
                    var actualNames = string.Join(", ", candidate.Placeholders.Select(Describe).Order(StringComparer.Ordinal));
                    violations.Add(new TranslationViolation(
                        TranslationViolationRule.PlaceholderMismatch,
                        key,
                        locale.Tag,
                        $"Key '{key}': placeholders in '{locale.Tag}' [{actualNames}] differ from '{ReferenceLocale.Tag}' [{expectedNames}] (SECURITY.md, Input validation rule 7)."));
                }
            }
        }

        return new TranslationValidationResult(violations);
    }

    private static string Describe(IcuPlaceholder placeholder) =>
        $"{placeholder.Name}:{placeholder.Kind}";
}

/// <summary>
/// Thrown when a resource set fails validation at registry creation: the set
/// is rejected as a whole — a blocking gate, no warn-only mode (CC-QA-006;
/// SECURITY.md, Input validation rule 1).
/// </summary>
public sealed class TranslationValidationException : Exception
{
    public TranslationValidationException(IReadOnlyList<TranslationViolation> violations)
        : base("Translation resources failed validation (CC-I18N-002):\n" +
               string.Join('\n', violations.Select(v => $"- [{v.Rule}] {v.Detail}")))
    {
        Violations = violations;
    }

    public IReadOnlyList<TranslationViolation> Violations { get; }
}
