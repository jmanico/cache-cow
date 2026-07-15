using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace CacheCow.Modules.ContentLocalization.Resources.MessageFormat;

/// <summary>
/// First-party recursive-descent parser for the ICU MessageFormat subset the
/// platform needs (CC-I18N-002): literal text with ICU apostrophe quoting,
/// <c>{name}</c>, <c>{n, plural, ...}</c> (categories and <c>=N</c> exact
/// matches, <c>#</c>), and <c>{x, select, ...}</c>. Everything else —
/// malformed syntax, unsupported argument types, HTML markup (any '&lt;'),
/// missing <c>other</c> branches, duplicate selectors, excessive nesting — is
/// rejected, never sanitized into acceptance (SECURITY.md, Input validation
/// rules 1 and 7). No third-party library is used (SECURITY.md, Dependency
/// rule 1); the full ICU feature surface is an open question on issue 064.
/// </summary>
public static class IcuMessageParser
{
    private const int MaxNestingDepth = 8;

    private static readonly string[] PluralCategories = ["zero", "one", "two", "few", "many", "other"];

    /// <summary>Parses and validates a resource string, throwing on any violation.</summary>
    public static IcuMessage Parse(string message) =>
        TryParse(message, out var parsed, out var error)
            ? parsed
            : throw new IcuMessageFormatException(error);

    /// <summary>
    /// Validates a resource string without throwing: hostile input yields a
    /// structured error, never a crash and never a partially accepted message.
    /// The error names the rule and position, not the raw content
    /// (SECURITY.md, Logging rule 5).
    /// </summary>
    public static bool TryParse(
        string? message,
        [NotNullWhen(true)] out IcuMessage? parsed,
        [NotNullWhen(false)] out string? error)
    {
        parsed = null;

        if (message is null)
        {
            error = "Resource value is null.";
            return false;
        }

        var htmlIndex = message.IndexOf('<');
        if (htmlIndex >= 0)
        {
            error = $"HTML markup is not permitted in string resources ('<' at position {htmlIndex}; SECURITY.md, Input validation rule 7).";
            return false;
        }

        try
        {
            var state = new ParserState(message);
            var parts = ParseMessage(state, depth: 0, insidePlural: false, stopAtCloseBrace: false);
            var placeholders = new HashSet<IcuPlaceholder>();
            CollectPlaceholders(parts, placeholders);
            parsed = new IcuMessage(parts, placeholders);
            error = null;
            return true;
        }
        catch (IcuMessageFormatException rejected)
        {
            error = rejected.Message;
            return false;
        }
    }

    private sealed class ParserState
    {
        public ParserState(string source)
        {
            Source = source;
        }

        public string Source { get; }

        public int Position { get; set; }

        public bool AtEnd => Position >= Source.Length;

        public char Current => Source[Position];
    }

    private static List<IcuMessagePart> ParseMessage(ParserState state, int depth, bool insidePlural, bool stopAtCloseBrace)
    {
        var parts = new List<IcuMessagePart>();
        var literal = new StringBuilder();

        void FlushLiteral()
        {
            if (literal.Length > 0)
            {
                parts.Add(new IcuLiteralPart(literal.ToString()));
                literal.Clear();
            }
        }

        while (!state.AtEnd)
        {
            var c = state.Current;

            if (c == '}' && stopAtCloseBrace)
            {
                break;
            }

            switch (c)
            {
                case '{':
                    FlushLiteral();
                    parts.Add(ParseArgument(state, depth, insidePlural));
                    break;

                case '}':
                    throw new IcuMessageFormatException($"Unmatched '}}' at position {state.Position}.");

                case '#' when insidePlural:
                    FlushLiteral();
                    parts.Add(IcuPoundPart.Instance);
                    state.Position++;
                    break;

                case '\'':
                    AppendQuoted(state, literal);
                    break;

                default:
                    literal.Append(c);
                    state.Position++;
                    break;
            }
        }

        FlushLiteral();
        return parts;
    }

    private static void AppendQuoted(ParserState state, StringBuilder literal)
    {
        // ICU apostrophe rules: '' is a literal apostrophe; a single quote
        // before a syntax character opens quoted literal text closed by the
        // next single quote; any other lone apostrophe is literal.
        var next = state.Position + 1;
        if (next < state.Source.Length && state.Source[next] == '\'')
        {
            literal.Append('\'');
            state.Position += 2;
            return;
        }

        if (next < state.Source.Length && state.Source[next] is '{' or '}' or '#')
        {
            state.Position++; // consume the opening quote
            while (!state.AtEnd)
            {
                if (state.Current == '\'')
                {
                    if (state.Position + 1 < state.Source.Length && state.Source[state.Position + 1] == '\'')
                    {
                        literal.Append('\'');
                        state.Position += 2;
                        continue;
                    }

                    state.Position++; // closing quote
                    return;
                }

                literal.Append(state.Current);
                state.Position++;
            }

            throw new IcuMessageFormatException("Unterminated quoted text; a quoted run must be closed with a single quote.");
        }

        literal.Append('\'');
        state.Position++;
    }

    private static IcuMessagePart ParseArgument(ParserState state, int depth, bool insidePlural)
    {
        if (depth >= MaxNestingDepth)
        {
            throw new IcuMessageFormatException($"Nesting deeper than {MaxNestingDepth} levels at position {state.Position}; rejecting (fail closed).");
        }

        state.Position++; // consume '{'
        SkipWhitespace(state);
        var name = ReadIdentifier(state, allowHyphen: false);
        if (name.Length == 0)
        {
            throw new IcuMessageFormatException($"Argument name expected at position {state.Position}.");
        }

        SkipWhitespace(state);
        if (state.AtEnd)
        {
            throw new IcuMessageFormatException("Unterminated argument; expected '}' or ','.");
        }

        if (state.Current == '}')
        {
            state.Position++;
            return new IcuTextArgumentPart(name);
        }

        if (state.Current != ',')
        {
            throw new IcuMessageFormatException($"Expected '}}' or ',' at position {state.Position}.");
        }

        state.Position++;
        SkipWhitespace(state);
        var argumentType = ReadIdentifier(state, allowHyphen: false);
        SkipWhitespace(state);

        if (argumentType is not ("plural" or "select"))
        {
            throw new IcuMessageFormatException(
                $"Unsupported argument type '{argumentType}'; the validated subset is placeholders, plural, and select (CC-I18N-002).");
        }

        if (state.AtEnd || state.Current != ',')
        {
            throw new IcuMessageFormatException($"Expected ',' and a {argumentType} style after the argument type.");
        }

        state.Position++;
        var isPlural = argumentType == "plural";
        var branches = ParseBranches(state, depth + 1, isPlural, insidePlural);

        return isPlural
            ? new IcuPluralArgumentPart(name, branches)
            : new IcuSelectArgumentPart(name, branches);
    }

    private static Dictionary<string, IcuBranch> ParseBranches(ParserState state, int depth, bool isPlural, bool outerInsidePlural)
    {
        var branches = new Dictionary<string, IcuBranch>(StringComparer.Ordinal);

        while (true)
        {
            SkipWhitespace(state);
            if (state.AtEnd)
            {
                throw new IcuMessageFormatException("Unterminated plural/select argument; expected '}'.");
            }

            if (state.Current == '}')
            {
                state.Position++;
                break;
            }

            var selector = ReadSelector(state, isPlural);
            SkipWhitespace(state);
            if (state.AtEnd || state.Current != '{')
            {
                throw new IcuMessageFormatException($"Expected '{{' after selector '{selector}'.");
            }

            state.Position++;
            var parts = ParseMessage(state, depth, insidePlural: isPlural || outerInsidePlural, stopAtCloseBrace: true);
            if (state.AtEnd || state.Current != '}')
            {
                throw new IcuMessageFormatException($"Unterminated branch for selector '{selector}'.");
            }

            state.Position++;

            if (!branches.TryAdd(selector, new IcuBranch(parts)))
            {
                throw new IcuMessageFormatException($"Duplicate selector '{selector}'.");
            }
        }

        if (!branches.ContainsKey("other"))
        {
            throw new IcuMessageFormatException("A plural/select argument must define an 'other' branch.");
        }

        return branches;
    }

    private static string ReadSelector(ParserState state, bool isPlural)
    {
        if (isPlural && state.Current == '=')
        {
            state.Position++;
            var digits = ReadDigits(state);
            if (digits.Length == 0)
            {
                throw new IcuMessageFormatException($"Exact plural selector '=' requires digits at position {state.Position}.");
            }

            return "=" + digits;
        }

        var selector = ReadIdentifier(state, allowHyphen: !isPlural);
        if (selector.Length == 0)
        {
            throw new IcuMessageFormatException($"Selector expected at position {state.Position}.");
        }

        if (isPlural && !PluralCategories.Contains(selector, StringComparer.Ordinal))
        {
            throw new IcuMessageFormatException($"Unknown plural category '{selector}'; expected zero/one/two/few/many/other or '=N'.");
        }

        return selector;
    }

    private static string ReadIdentifier(ParserState state, bool allowHyphen)
    {
        var start = state.Position;
        while (!state.AtEnd)
        {
            var c = state.Current;
            var isStart = state.Position == start;
            var valid = char.IsAsciiLetter(c)
                || c == '_'
                || (!isStart && char.IsAsciiDigit(c))
                || (!isStart && allowHyphen && c == '-');
            if (!valid)
            {
                break;
            }

            state.Position++;
        }

        return state.Source[start..state.Position];
    }

    private static string ReadDigits(ParserState state)
    {
        var start = state.Position;
        while (!state.AtEnd && char.IsAsciiDigit(state.Current))
        {
            state.Position++;
        }

        return state.Source[start..state.Position];
    }

    private static void SkipWhitespace(ParserState state)
    {
        while (!state.AtEnd && char.IsWhiteSpace(state.Current))
        {
            state.Position++;
        }
    }

    private static void CollectPlaceholders(IReadOnlyList<IcuMessagePart> parts, HashSet<IcuPlaceholder> placeholders)
    {
        foreach (var part in parts)
        {
            switch (part)
            {
                case IcuTextArgumentPart text:
                    placeholders.Add(new IcuPlaceholder(text.Name, IcuPlaceholderKind.Text));
                    break;

                case IcuPluralArgumentPart plural:
                    placeholders.Add(new IcuPlaceholder(plural.Name, IcuPlaceholderKind.Plural));
                    foreach (var branch in plural.Branches.Values)
                    {
                        CollectPlaceholders(branch.Parts, placeholders);
                    }

                    break;

                case IcuSelectArgumentPart select:
                    placeholders.Add(new IcuPlaceholder(select.Name, IcuPlaceholderKind.Select));
                    foreach (var branch in select.Branches.Values)
                    {
                        CollectPlaceholders(branch.Parts, placeholders);
                    }

                    break;

                default:
                    break;
            }
        }
    }
}
