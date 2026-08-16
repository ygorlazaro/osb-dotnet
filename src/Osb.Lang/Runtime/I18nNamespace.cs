using System.Globalization;
using System.IO;
using System.Collections.Concurrent;
using System.Linq;
using Osb.Lang.Diagnostics;

namespace Osb.Lang.Runtime;

/// <summary>
/// OSLANG 0.6 OSL.I18N standard library implementation.
/// </summary>
public static class I18nNamespace
{
    private static readonly ConcurrentDictionary<string, I18nResource> _resources = new(StringComparer.OrdinalIgnoreCase);
    private static string _activeLanguage = "EN-US";
    private static string _defaultLanguage = "EN-US";
    private static string _fallbackLanguage = "EN-US";

    static I18nNamespace()
    {
        _resources.TryAdd("EN-US", new I18nResource("EN-US", []));
        _resources.TryAdd("PT-BR", new I18nResource("PT-BR", []));
        TryLoadDefaultResources();
    }

    private static void TryLoadDefaultResources()
    {
        try
        {
            var appDir = AppContext.BaseDirectory;
            var langEntries = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            void AddEntries(string lang, IEnumerable<KeyValuePair<string, string>> entries)
            {
                if (!langEntries.TryGetValue(lang, out var dict))
                {
                    dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    langEntries[lang] = dict;
                }

                foreach (var entry in entries)
                {
                    dict[entry.Key] = entry.Value;
                }
            }

            var i18nDir = Path.Combine(appDir, "I18N");
            if (Directory.Exists(i18nDir))
            {
                foreach (var file in Directory.GetFiles(i18nDir, "*.I18N").Concat(Directory.GetFiles(i18nDir, "*.i18n")))
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var dotIndex = fileName.IndexOf('.');
                    var lang = dotIndex >= 0 ? fileName[(dotIndex + 1)..].ToUpperInvariant() : fileName.ToUpperInvariant();
                    if (string.IsNullOrEmpty(lang))
                    {
                        continue;
                    }

                    try
                    {
                        var entries = LoadEntriesFromPath(file, SourceLocation.Unknown, prefix: null);
                        AddEntries(lang, entries);
                    }
                    catch
                    {
                    }
                }
            }

            var subDirs = Directory.GetDirectories(appDir, "I18N", SearchOption.AllDirectories);
            foreach (var subDir in subDirs)
            {
                var prefix = Path.GetFileName(Path.GetDirectoryName(subDir))?.ToLowerInvariant();
                if (string.IsNullOrEmpty(prefix))
                {
                    continue;
                }

                foreach (var file in Directory.GetFiles(subDir, "*.I18N").Concat(Directory.GetFiles(subDir, "*.i18n")))
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var dotIndex = fileName.IndexOf('.');
                    var lang = dotIndex >= 0 ? fileName[(dotIndex + 1)..].ToUpperInvariant() : fileName.ToUpperInvariant();
                    if (string.IsNullOrEmpty(lang))
                    {
                        continue;
                    }

                    try
                    {
                        var entries = LoadEntriesFromPath(file, SourceLocation.Unknown, prefix);
                        AddEntries(lang, entries);
                    }
                    catch
                    {
                    }
                }
            }

            foreach (var kvp in langEntries)
            {
                try
                {
                    var resource = new I18nResource(kvp.Key, kvp.Value.Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value)));
                    _resources.AddOrUpdate(kvp.Key, resource, (_, _) => resource);
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }

    public static OslangValue Call(string methodName, IReadOnlyList<OslangValue> args, SourceLocation location, string? basePath = null)
    {
        var upper = methodName.ToUpperInvariant();
        switch (upper)
        {
            case "GET":
                return Get(args, location, basePath);
            case "HAS":
                return Has(args, location, basePath);
            case "KEYS":
                return Keys(args, location, basePath);
            case "LANGUAGE":
                return Language(args, location);
            case "SETLANGUAGE":
                return SetLanguage(args, location, basePath);
            case "LANGUAGES":
                return Languages(args, location);
            case "LOAD":
                return Load(args, location, basePath);
            case "LOADLANGUAGE":
                return LoadLanguage(args, location, basePath);
            case "RELOAD":
                return Reload(args, location, basePath);
            case "UNLOAD":
                return Unload(args, location);
            case "DEFAULT":
                return Default(args, location);
            case "SETDEFAULT":
                return SetDefault(args, location);
            case "SETFALLBACK":
                return SetFallback(args, location);
            default:
                throw new OslangRuntimeException(location, $"Unknown OSL.I18N method '{methodName}'.");
        }
    }

    private static OslangValue Get(IReadOnlyList<OslangValue> args, SourceLocation location, string? basePath)
    {
        if (args.Count < 1)
        {
            throw new OslangRuntimeException(location, "I18N.GET() expects at least 1 argument (key).");
        }

        var key = AsString(args[0], "I18N.GET");
        var template = ResolveTemplate(key, basePath);

        if (template == key)
        {
            if (args.Count > 1)
            {
                template = AsString(args[1], "I18N.GET");
            }
        }

        if (args.Count > (template == key ? 2 : 1))
        {
            var formatArgs = args.Skip(template == key ? 2 : 1)
                .Select(a => Conversions.ToDisplayString(a, location))
                .ToArray();
            try
            {
                template = string.Format(CultureInfo.InvariantCulture, template, formatArgs);
            }
            catch
            {
            }
        }

        return new StringValue(template);
    }

    private static OslangValue Has(IReadOnlyList<OslangValue> args, SourceLocation location, string? basePath)
    {
        if (args.Count != 1)
        {
            throw new OslangRuntimeException(location, "I18N.HAS() expects exactly 1 argument (key).");
        }

        var key = AsString(args[0], "I18N.HAS");
        var template = ResolveTemplate(key, basePath);
        return BooleanValue.Of(template != key);
    }

    private static OslangValue Keys(IReadOnlyList<OslangValue> args, SourceLocation location, string? basePath)
    {
        RequireArgCount(args, 0, "I18N.KEYS", location);
        var lang = _activeLanguage;
        if (!_resources.TryGetValue(lang, out var resource))
        {
            resource = _resources.GetOrAdd(lang, l => new I18nResource(l, LoadEntriesFromDisk(l, basePath)));
        }

        return new ArrayValue(resource.Keys.Select(k => (OslangValue)new StringValue(k)).ToList(), RuntimeType.String);
    }

    private static OslangValue Language(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        RequireArgCount(args, 0, "I18N.LANGUAGE", location);
        return new StringValue(_activeLanguage);
    }

    private static OslangValue SetLanguage(IReadOnlyList<OslangValue> args, SourceLocation location, string? basePath)
    {
        if (args.Count != 1)
        {
            throw new OslangRuntimeException(location, "I18N.SETLANGUAGE() expects exactly 1 argument (language).");
        }

        var lang = AsString(args[0], "I18N.SETLANGUAGE");
        _activeLanguage = lang;
        EnsureResourceLoaded(lang, basePath);
        return OslangValue.Null;
    }

    private static OslangValue Languages(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        RequireArgCount(args, 0, "I18N.LANGUAGES", location);
        var discovered = DiscoverAvailableLanguages();
        var allLangs = _resources.Keys.Union(discovered).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return new ArrayValue(allLangs.Select(k => (OslangValue)new StringValue(k)).ToList(), RuntimeType.String);
    }

    private static IEnumerable<string> DiscoverAvailableLanguages()
    {
        var dirs = new List<string?>();
        var appDir = AppContext.BaseDirectory;
        dirs.Add(Path.Combine(appDir, "I18N"));

        foreach (var dir in dirs)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                continue;
            }

            foreach (var file in Directory.GetFiles(dir, "*.I18N").Concat(Directory.GetFiles(dir, "*.i18n")))
            {
                var lang = Path.GetFileNameWithoutExtension(file);
                var dotIndex = lang.IndexOf('.');
                if (dotIndex >= 0)
                {
                    yield return lang[(dotIndex + 1)..].ToUpperInvariant();
                }
                else
                {
                    yield return lang.ToUpperInvariant();
                }
            }
        }
    }

    private static OslangValue Load(IReadOnlyList<OslangValue> args, SourceLocation location, string? basePath)
    {
        if (args.Count != 1)
        {
            throw new OslangRuntimeException(location, "I18N.LOAD() expects exactly 1 argument (path).");
        }

        var path = AsString(args[0], "I18N.LOAD");
        var lang = InferLanguageFromPath(path);
        var entries = LoadEntriesFromPath(path, location);
        var resource = new I18nResource(lang, entries);
        _resources.AddOrUpdate(lang, resource, (_, _) => resource);
        return OslangValue.Null;
    }

    private static OslangValue LoadLanguage(IReadOnlyList<OslangValue> args, SourceLocation location, string? basePath)
    {
        if (args.Count != 2)
        {
            throw new OslangRuntimeException(location, "I18N.LOADLANGUAGE() expects exactly 2 arguments (language, path).");
        }

        var lang = AsString(args[0], "I18N.LOADLANGUAGE");
        var path = AsString(args[1], "I18N.LOADLANGUAGE");
        var entries = LoadEntriesFromPath(path, location);
        var resource = new I18nResource(lang, entries);
        _resources.AddOrUpdate(lang, resource, (_, _) => resource);
        return OslangValue.Null;
    }

    private static OslangValue Reload(IReadOnlyList<OslangValue> args, SourceLocation location, string? basePath)
    {
        if (args.Count > 1)
        {
            throw new OslangRuntimeException(location, "I18N.RELOAD() expects 0 or 1 argument (language).");
        }

        if (args.Count == 0)
        {
            var currentLang = _activeLanguage;
            ReloadLanguage(currentLang, basePath);
            return OslangValue.Null;
        }

        var reloadLang = AsString(args[0], "I18N.RELOAD");
        ReloadLanguage(reloadLang, basePath);
        return OslangValue.Null;
    }

    private static OslangValue Unload(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count != 1)
        {
            throw new OslangRuntimeException(location, "I18N.UNLOAD() expects exactly 1 argument (language).");
        }

        var lang = AsString(args[0], "I18N.UNLOAD");
        _resources.TryRemove(lang, out _);
        return OslangValue.Null;
    }

    private static OslangValue Default(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        RequireArgCount(args, 0, "I18N.DEFAULT", location);
        return new StringValue(_defaultLanguage);
    }

    private static OslangValue SetDefault(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count != 1)
        {
            throw new OslangRuntimeException(location, "I18N.SETDEFAULT() expects exactly 1 argument (language).");
        }

        _defaultLanguage = AsString(args[0], "I18N.SETDEFAULT");
        return OslangValue.Null;
    }

    private static OslangValue SetFallback(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count != 1)
        {
            throw new OslangRuntimeException(location, "I18N.SETFALLBACK() expects exactly 1 argument (language or NULL).");
        }

        if (args[0] is NullValue)
        {
            _fallbackLanguage = null!;
            return OslangValue.Null;
        }

        _fallbackLanguage = AsString(args[0], "I18N.SETFALLBACK");
        return OslangValue.Null;
    }

    private static string ResolveTemplate(string key, string? basePath)
    {
        var lang = _activeLanguage;
        if (_resources.TryGetValue(lang, out var resource) && resource.TryGet(key, out var template))
        {
            return template;
        }

        if (!string.IsNullOrEmpty(basePath))
        {
            EnsureResourceLoaded(lang, basePath);
            if (_resources.TryGetValue(lang, out resource) && resource.TryGet(key, out template))
            {
                return template;
            }
        }

        if (!string.IsNullOrEmpty(_fallbackLanguage) && _resources.TryGetValue(_fallbackLanguage, out var fallback) && fallback.TryGet(key, out template))
        {
            return template;
        }

        if (lang != _defaultLanguage && _resources.TryGetValue(_defaultLanguage, out var defaultResource) && defaultResource.TryGet(key, out template))
        {
            return template;
        }

        if (_resources.TryGetValue("EN-US", out var enResource) && enResource.TryGet(key, out template))
        {
            return template;
        }

        return key;
    }

    private static void EnsureResourceLoaded(string lang, string? basePath)
    {
        var entries = LoadEntriesFromDisk(lang, basePath);
        if (!entries.Any())
        {
            return;
        }

        var resource = new I18nResource(lang, entries);
        _resources.AddOrUpdate(lang, resource, (_, _) => resource);
    }

    private static IEnumerable<KeyValuePair<string, string>> LoadEntriesFromDisk(string lang, string? basePath)
    {
        string? prefix = null;
        if (!string.IsNullOrEmpty(basePath))
        {
            var trimmed = basePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var appName = Path.GetFileName(trimmed);
            if (!string.IsNullOrEmpty(appName))
            {
                prefix = appName.ToLowerInvariant();
            }
        }

        var dirs = new List<string?>();
        if (!string.IsNullOrEmpty(basePath))
        {
            dirs.Add(Path.Combine(basePath, "I18N"));
        }

        var appDir = AppContext.BaseDirectory;
        dirs.Add(Path.Combine(appDir, "I18N"));

        foreach (var dir in dirs)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                continue;
            }

            var upperPath = Path.Combine(dir, lang + ".I18N");
            if (File.Exists(upperPath))
            {
                return LoadEntriesFromPath(upperPath, SourceLocation.Unknown, prefix);
            }

            var lowerPath = Path.Combine(dir, lang + ".i18n");
            if (File.Exists(lowerPath))
            {
                return LoadEntriesFromPath(lowerPath, SourceLocation.Unknown, prefix);
            }
        }

        return [];
    }

    private static IEnumerable<KeyValuePair<string, string>> LoadEntriesFromPath(string path, SourceLocation location, string? prefix = null)
    {
        if (!File.Exists(path))
        {
            throw new OslangRuntimeException(location, $"I18N resource file not found: '{path}'.");
        }

        var entries = new List<KeyValuePair<string, string>>();
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq < 1)
            {
                throw new OslangRuntimeException(location, $"Invalid I18N resource syntax in '{path}': '{rawLine}'.");
            }

            var key = line[..eq].Trim();
            if (string.IsNullOrEmpty(key))
            {
                throw new OslangRuntimeException(location, $"Empty I18N key in '{path}'.");
            }

            if (!string.IsNullOrEmpty(prefix))
            {
                key = $"{prefix}.{key}";
            }

            var value = line[(eq + 1)..];
            entries.Add(new KeyValuePair<string, string>(key, value));
        }

        return entries;
    }

    private static void ReloadLanguage(string lang, string? basePath)
    {
        var entries = LoadEntriesFromDisk(lang, basePath);
        var resource = new I18nResource(lang, entries);
        _resources.AddOrUpdate(lang, resource, (_, _) => resource);
    }

    private static string InferLanguageFromPath(string path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        var dotIndex = fileName.IndexOf('.');
        if (dotIndex >= 0)
        {
            return fileName[(dotIndex + 1)..].ToUpperInvariant();
        }

        return fileName.ToUpperInvariant();
    }

    private static string AsString(OslangValue value, string fn)
    {
        if (value is StringValue sv)
        {
            return sv.Value;
        }

        throw new OslangRuntimeException(SourceLocation.Unknown, $"{fn}() expects a STRING argument, got {value.TypeName}.");
    }

    private static void RequireArgCount(IReadOnlyList<OslangValue> args, int expected, string fn, SourceLocation location)
    {
        if (args.Count != expected)
        {
            throw new OslangRuntimeException(location, $"{fn}() expects {expected} argument(s), got {args.Count}.");
        }
    }
}
