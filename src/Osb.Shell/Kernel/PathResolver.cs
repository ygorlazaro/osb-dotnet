namespace Osb.Shell.Kernel;

/// <summary>
/// O DOS original era case-insensitive (CD pasta1 e CD PASTA1 são a mesma coisa).
/// Linux/macOS não são. Este helper resolve um caminho digitado pelo usuário contra
/// o que realmente existe em disco, ignorando maiúsculas/minúsculas segmento por
/// segmento - assim comandos como CD, TYPE, DEL etc. se comportam do jeito que
/// alguém acostumado com DOS/Windows espera, mesmo num filesystem case-sensitive.
/// Se não achar uma correspondência (ex: caminho não existe ainda, como em MD),
/// devolve o caminho original sem mexer.
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
