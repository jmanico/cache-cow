namespace CacheCow.Modules.MarketGating.Resolution;

/// <summary>
/// Opaque identity a market/locale preference is stored against — a guest
/// session identifier or an account identifier; the module does not interpret
/// it. Which identifier wins when both exist is an open question in issue 024.
/// </summary>
public sealed record PreferenceSubject
{
    public PreferenceSubject(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }
}
