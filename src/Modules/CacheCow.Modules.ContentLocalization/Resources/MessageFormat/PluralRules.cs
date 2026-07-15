using CacheCow.SharedKernel;

namespace CacheCow.Modules.ContentLocalization.Resources.MessageFormat;

/// <summary>
/// Minimal CLDR plural-category selection for the seven launch locales
/// (CC-I18N-001): en/es/de select <c>one</c> when n == 1; ja is
/// <c>other</c>-only; hi selects <c>one</c> when n == 0 or n == 1. Unknown
/// languages conservatively use n == 1 → <c>one</c>. The full CLDR rule set
/// is outside the first-party subset; extending it is tracked with the issue
/// 064 open question on the full ICU feature surface.
/// </summary>
internal static class PluralRules
{
    public static string SelectCategory(Locale locale, decimal operand)
    {
        var n = Math.Abs(operand);
        var language = LanguageSubtag(locale);

        return language switch
        {
            "ja" => "other",
            "hi" => n == 0m || n == 1m ? "one" : "other",
            _ => n == 1m ? "one" : "other",
        };
    }

    private static string LanguageSubtag(Locale locale)
    {
        var tag = locale.Tag;
        var separator = tag.IndexOf('-', StringComparison.Ordinal);
        return separator < 0 ? tag : tag[..separator];
    }
}
