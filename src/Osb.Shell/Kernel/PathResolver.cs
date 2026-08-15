namespace Osb.Shell.Kernel;

/// <summary>
/// Resolves user-provided paths against the filesystem in a case-insensitive way,
/// matching the behavior expected from traditional command-line shells.
/// </summary>
public static class PathResolver
{
    public static string Resolve(string userPath)
    {
        if (string.IsNullOrEmpty(userPath))
        {
            return userPath;
        }

        var isRooted = Path.IsPathRooted(userPath);
        var basePath = isRooted ? Path.GetPathRoot(userPath)! : Directory.GetCurrentDirectory();
        var relative = isRooted ? userPath[basePath.Length..] : userPath;

        var segments = relative.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        var current = basePath;

        foreach (var segment in segments)
        {
            if (segment is "." or "..")
            {
                current = Path.Combine(current, segment);
                continue;
            }

            var match = FindEntryIgnoreCase(current, segment);
            current = Path.Combine(current, match ?? segment);
        }

        return current;
    }

    private static string? FindEntryIgnoreCase(string dir, string name)
    {
        if (!Directory.Exists(dir))
        {
            return null;
        }

        try
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(dir))
            {
                var entryName = Path.GetFileName(entry);
                if (string.Equals(entryName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return entryName;
                }
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        return null;
    }
}
