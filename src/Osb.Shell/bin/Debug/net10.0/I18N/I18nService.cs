using System.Globalization;

namespace Osb.Shell.Kernel;

/// <summary>
/// Simple I18N engine for OSB shell.
/// Loads key=value translation files from the I18N folder.
/// Falls back to EN-US when a key is missing in the selected language.
/// </summary>
public static class I18nService
{
    private static readonly Dictionary<string, Dictionary<string, string>> _translations = new(StringComparer.OrdinalIgnoreCase);
    private static string _currentLanguage = "EN-US";

    static I18nService()
    {
        LoadLanguage("EN-US");
        LoadLanguage("PT-BR");
    }

    public static string CurrentLanguage => _currentLanguage;

    public static void SetLanguage(string language)
    {
        var normalized = language.Trim().ToUpperInvariant();
        if (_translations.ContainsKey(normalized))
        {
            _currentLanguage = normalized;
        }
    }

    public static string Get(string key, params object[] args)
    {
        var template = GetTemplate(key);
        if (template is null)
        {
            return key;
        }

        if (args.Length == 0)
        {
            return template;
        }

        return string.Format(CultureInfo.InvariantCulture, template, args);
    }

    private static string? GetTemplate(string key)
    {
        if (_translations.TryGetValue(_currentLanguage, out var current) && current.TryGetValue(key, out var template))
        {
            return template;
        }

        if (_currentLanguage != "EN-US" && _translations.TryGetValue("EN-US", out var fallback) && fallback.TryGetValue(key, out var fallbackTemplate))
        {
            return fallbackTemplate;
        }

        return null;
    }

    private static void LoadLanguage(string language)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "I18N", language + ".i18n");
        if (!File.Exists(path))
        {
            return;
        }

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
            {
                continue;
            }

            var equalsIndex = trimmed.IndexOf('=');
            if (equalsIndex < 1)
            {
                continue;
            }

            var k = trimmed[..equalsIndex].Trim();
            var v = trimmed[(equalsIndex + 1)..].Trim();
            if (!string.IsNullOrEmpty(k))
            {
                dict[k] = v;
            }
        }

        _translations[language] = dict;
    }
}
