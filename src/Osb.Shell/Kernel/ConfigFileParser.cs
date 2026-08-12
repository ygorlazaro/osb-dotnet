namespace Osb.Shell.Kernel;

public static class ConfigFileParser
{
    public sealed record Entry(string Name, string Description);

    public static IReadOnlyList<Entry> LoadEntries(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var lines = File.ReadAllLines(path);
        var entries = new List<Entry>();

        for (var i = 0; i < lines.Length; i++)
        {
            var rawLine = lines[i].Trim();
            if (string.IsNullOrEmpty(rawLine))
            {
                continue;
            }

            if (rawLine.StartsWith(";"))
            {
                continue;
            }

            if (rawLine.StartsWith("-ENDOFFILE", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (rawLine.StartsWith("-ENDCOMMAND", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var name = rawLine.StartsWith($"-", StringComparison.Ordinal) ? rawLine[1..].Trim() : rawLine;

            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var description = string.Empty;
            if (i + 1 < lines.Length)
            {
                var candidate = lines[i + 1].Trim();
                if (!string.IsNullOrEmpty(candidate) && !candidate.StartsWith("-") && !candidate.StartsWith(";"))
                {
                    description = candidate;
                    i++;
                }
            }

            entries.Add(new Entry(name.ToUpperInvariant(), description));
        }

        return entries;
    }
}
