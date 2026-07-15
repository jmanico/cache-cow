using System.Text.RegularExpressions;

namespace CacheCow.SharedKernel;

/// <summary>
/// A BCP 47 language/region tag controlling UI strings and formatting
/// (REQUIREMENTS.md §2). Validates well-formedness of the langtag core
/// (language, optional script, optional region, optional variants) and
/// canonicalizes casing; malformed tags and tags with extensions/private-use
/// subtags are rejected rather than normalized into acceptance (SECURITY.md,
/// Input validation rule 1). The launch-locale allowlist (CC-I18N-001) is
/// Content &amp; Localization policy, not a property of this type (issue 003,
/// Open Questions).
/// Independent of <see cref="Market"/> by construction (CC-MKT-002, CC-SEC-012).
/// </summary>
public readonly partial struct Locale : IEquatable<Locale>
{
    [GeneratedRegex(
        "^(?<language>[a-zA-Z]{2,3})" +
        "(?:-(?<script>[a-zA-Z]{4}))?" +
        "(?:-(?<region>[a-zA-Z]{2}|[0-9]{3}))?" +
        "(?:-(?<variant>[a-zA-Z0-9]{5,8}|[0-9][a-zA-Z0-9]{3}))*$",
        RegexOptions.ExplicitCapture)]
    private static partial Regex LangtagPattern();

    private readonly string? _tag;

    private Locale(string canonicalTag)
    {
        _tag = canonicalTag;
    }

    /// <summary>Canonical string form: lowercase language, titlecase script, uppercase region.</summary>
    public string Tag =>
        _tag ?? throw new InvalidOperationException(
            "Uninitialized Locale value; use Parse or TryParse.");

    public static bool TryParse(string? tag, out Locale locale)
    {
        locale = default;
        if (string.IsNullOrEmpty(tag))
        {
            return false;
        }

        var match = LangtagPattern().Match(tag);
        if (!match.Success)
        {
            return false;
        }

        var parts = new List<string>
        {
            match.Groups["language"].Value.ToLowerInvariant(),
        };

        if (match.Groups["script"].Success)
        {
            var script = match.Groups["script"].Value;
            parts.Add(char.ToUpperInvariant(script[0]) + script[1..].ToLowerInvariant());
        }

        if (match.Groups["region"].Success)
        {
            parts.Add(match.Groups["region"].Value.ToUpperInvariant());
        }

        foreach (Capture variant in match.Groups["variant"].Captures)
        {
            parts.Add(variant.Value.ToLowerInvariant());
        }

        locale = new Locale(string.Join('-', parts));
        return true;
    }

    public static Locale Parse(string tag) =>
        TryParse(tag, out var locale)
            ? locale
            : throw new FormatException($"'{tag}' is not a well-formed BCP 47 langtag (REQUIREMENTS.md §2).");

    public bool Equals(Locale other) => string.Equals(_tag, other._tag, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is Locale other && Equals(other);

    public override int GetHashCode() => _tag?.GetHashCode(StringComparison.Ordinal) ?? 0;

    public static bool operator ==(Locale left, Locale right) => left.Equals(right);

    public static bool operator !=(Locale left, Locale right) => !left.Equals(right);

    public override string ToString() => Tag;
}
