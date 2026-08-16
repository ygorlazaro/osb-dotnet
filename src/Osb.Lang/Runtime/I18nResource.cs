using System.Collections.Concurrent;
using Osb.Lang.Diagnostics;

namespace Osb.Lang.Runtime;

/// <summary>
/// Represents a loaded .I18N resource file: key → template strings,
/// with duplicate-key detection and case-insensitive lookup.
/// </summary>
internal sealed class I18nResource
{
    private readonly ConcurrentDictionary<string, string> _templates = new(StringComparer.OrdinalIgnoreCase);

    public string Language { get; }
    public IReadOnlyCollection<string> Keys => _templates.Keys.ToList();

    public I18nResource(string language, IEnumerable<KeyValuePair<string, string>> entries)
    {
        Language = language;
        foreach (var kvp in entries)
        {
            if (_templates.ContainsKey(kvp.Key))
            {
                throw new OslangRuntimeException(SourceLocation.Unknown, $"Duplicate I18N key '{kvp.Key}' in resource for language '{language}'.");
            }

            _templates[kvp.Key] = kvp.Value;
        }
    }

    public bool TryGet(string key, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out string template)
    {
        return _templates.TryGetValue(key, out template);
    }
}
