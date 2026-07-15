using System.Globalization;
using System.Text;
using CacheCow.SharedKernel;

namespace CacheCow.Modules.ContentLocalization.Resources.MessageFormat;

/// <summary>The kind of an ICU placeholder, used for cross-locale parity checks (CC-I18N-002).</summary>
public enum IcuPlaceholderKind
{
    Text,
    Plural,
    Select,
}

/// <summary>A placeholder's identity for parity comparison: name plus kind.</summary>
public sealed record IcuPlaceholder(string Name, IcuPlaceholderKind Kind);

/// <summary>
/// A validated, parsed ICU MessageFormat message (subset: text placeholders,
/// plural, select — CC-I18N-002). Formatting is escape-by-default: interpolated
/// values are inserted as plain text and are never re-parsed as ICU syntax or
/// interpreted as markup — there is no opt-out to raw HTML anywhere in the
/// message pipeline (SECURITY.md, Input validation rules 5 and 7).
/// </summary>
public sealed class IcuMessage
{
    private readonly IReadOnlyList<IcuMessagePart> _parts;
    private readonly IReadOnlySet<IcuPlaceholder> _placeholders;

    internal IcuMessage(IReadOnlyList<IcuMessagePart> parts, IReadOnlySet<IcuPlaceholder> placeholders)
    {
        _parts = parts;
        _placeholders = placeholders;
    }

    /// <summary>The full placeholder set, including placeholders nested inside plural/select branches.</summary>
    public IReadOnlyCollection<IcuPlaceholder> Placeholders => _placeholders;

    /// <summary>
    /// Formats the message for the given locale. Missing or wrongly-typed
    /// arguments fail closed with <see cref="IcuMessageArgumentException"/> —
    /// a partially interpolated message is never produced.
    /// </summary>
    public string Format(Locale locale, IReadOnlyDictionary<string, object?> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var culture = CultureInfo.GetCultureInfo(locale.Tag);
        var builder = new StringBuilder();
        AppendParts(builder, _parts, culture, locale, arguments, pluralOperand: null);
        return builder.ToString();
    }

    private static void AppendParts(
        StringBuilder builder,
        IReadOnlyList<IcuMessagePart> parts,
        CultureInfo culture,
        Locale locale,
        IReadOnlyDictionary<string, object?> arguments,
        decimal? pluralOperand)
    {
        foreach (var part in parts)
        {
            switch (part)
            {
                case IcuLiteralPart literal:
                    builder.Append(literal.Text);
                    break;

                case IcuTextArgumentPart text:
                    builder.Append(TextValue(text.Name, arguments, culture));
                    break;

                case IcuPoundPart:
                    builder.Append(FormatNumber(
                        pluralOperand ?? throw new IcuMessageArgumentException(
                            "'#' outside a plural branch; the parser should have rejected this message."),
                        culture));
                    break;

                case IcuPluralArgumentPart plural:
                {
                    var operand = NumericValue(plural.Name, arguments);
                    var branch = SelectPluralBranch(plural, locale, operand);
                    AppendParts(builder, branch.Parts, culture, locale, arguments, operand);
                    break;
                }

                case IcuSelectArgumentPart select:
                {
                    var branch = SelectBranch(select, arguments);
                    AppendParts(builder, branch.Parts, culture, locale, arguments, pluralOperand);
                    break;
                }

                default:
                    throw new IcuMessageArgumentException(
                        $"Unknown message part '{part.GetType().Name}'; failing closed (SECURITY.md, Logging rule 2).");
            }
        }
    }

    private static string TextValue(string name, IReadOnlyDictionary<string, object?> arguments, CultureInfo culture)
    {
        if (!arguments.TryGetValue(name, out var value) || value is null)
        {
            throw new IcuMessageArgumentException($"Missing argument '{name}'; refusing to render a broken message (CC-I18N-006).");
        }

        // Escape-by-default: the value is returned as plain text. It is never
        // re-parsed as ICU syntax and never interpreted as markup (SECURITY.md,
        // Input validation rule 7).
        return value switch
        {
            string text => text,
            IFormattable formattable => formattable.ToString(null, culture),
            _ => throw new IcuMessageArgumentException(
                $"Argument '{name}' has unsupported type '{value.GetType().Name}'; pass a string or a formattable value."),
        };
    }

    private static decimal NumericValue(string name, IReadOnlyDictionary<string, object?> arguments)
    {
        if (!arguments.TryGetValue(name, out var value) || value is null)
        {
            throw new IcuMessageArgumentException($"Missing argument '{name}'; refusing to render a broken message (CC-I18N-006).");
        }

        return value switch
        {
            int i => i,
            long l => l,
            short s => s,
            byte b => b,
            uint u => u,
            ulong ul => ul,
            decimal d => d,
            double dbl => (decimal)dbl,
            float f => (decimal)f,
            _ => throw new IcuMessageArgumentException(
                $"Plural argument '{name}' must be numeric; got '{value.GetType().Name}'."),
        };
    }

    private static IcuBranch SelectPluralBranch(IcuPluralArgumentPart plural, Locale locale, decimal operand)
    {
        var exactSelector = "=" + operand.ToString(CultureInfo.InvariantCulture);
        if (plural.Branches.TryGetValue(exactSelector, out var exact))
        {
            return exact;
        }

        var category = PluralRules.SelectCategory(locale, operand);
        return plural.Branches.TryGetValue(category, out var branch)
            ? branch
            : plural.Branches["other"];
    }

    private static IcuBranch SelectBranch(IcuSelectArgumentPart select, IReadOnlyDictionary<string, object?> arguments)
    {
        if (!arguments.TryGetValue(select.Name, out var value) || value is not string selector)
        {
            throw new IcuMessageArgumentException(
                $"Select argument '{select.Name}' must be a string; refusing to render a broken message (CC-I18N-006).");
        }

        // The value only chooses a branch; it is never emitted, so a hostile
        // selector cannot inject content.
        return select.Branches.TryGetValue(selector, out var branch)
            ? branch
            : select.Branches["other"];
    }

    private static string FormatNumber(decimal operand, CultureInfo culture) =>
        operand.ToString("#,0.###", culture);
}
