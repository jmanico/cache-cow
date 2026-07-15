namespace CacheCow.Modules.ContentLocalization.Resources.MessageFormat;

/// <summary>
/// Internal AST for the first-party ICU MessageFormat subset (CC-I18N-002):
/// literal text, <c>{name}</c> placeholders, <c>{n, plural, ...}</c>, and
/// <c>{x, select, ...}</c>. There is deliberately no node for markup — a
/// message can only ever produce plain text (SECURITY.md, Input validation
/// rule 7: no HTML in string resources).
/// </summary>
internal abstract class IcuMessagePart
{
    private protected IcuMessagePart()
    {
    }
}

/// <summary>Literal text, emitted verbatim.</summary>
internal sealed class IcuLiteralPart : IcuMessagePart
{
    public IcuLiteralPart(string text)
    {
        Text = text;
    }

    public string Text { get; }
}

/// <summary>A plain <c>{name}</c> placeholder; the value is inserted as text.</summary>
internal sealed class IcuTextArgumentPart : IcuMessagePart
{
    public IcuTextArgumentPart(string name)
    {
        Name = name;
    }

    public string Name { get; }
}

/// <summary>The <c>#</c> token inside a plural branch: the plural operand as a locale-formatted number.</summary>
internal sealed class IcuPoundPart : IcuMessagePart
{
    public static IcuPoundPart Instance { get; } = new();

    private IcuPoundPart()
    {
    }
}

/// <summary>One selector branch of a plural or select argument.</summary>
internal sealed class IcuBranch
{
    public IcuBranch(IReadOnlyList<IcuMessagePart> parts)
    {
        Parts = parts;
    }

    public IReadOnlyList<IcuMessagePart> Parts { get; }
}

/// <summary>A <c>{name, plural, ...}</c> argument with exact (<c>=N</c>) and category selectors.</summary>
internal sealed class IcuPluralArgumentPart : IcuMessagePart
{
    public IcuPluralArgumentPart(string name, IReadOnlyDictionary<string, IcuBranch> branches)
    {
        Name = name;
        Branches = branches;
    }

    public string Name { get; }

    public IReadOnlyDictionary<string, IcuBranch> Branches { get; }
}

/// <summary>A <c>{name, select, ...}</c> argument.</summary>
internal sealed class IcuSelectArgumentPart : IcuMessagePart
{
    public IcuSelectArgumentPart(string name, IReadOnlyDictionary<string, IcuBranch> branches)
    {
        Name = name;
        Branches = branches;
    }

    public string Name { get; }

    public IReadOnlyDictionary<string, IcuBranch> Branches { get; }
}
