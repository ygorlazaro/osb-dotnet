using System.Diagnostics;
using System.Linq;
using Osb.Lang;

namespace Osb.Shell.Kernel;

public partial class OsbShell
{
    private static (string Path, string[] Args) ParseOshArgs(string args)
    {
        var trimmed = args.Trim();
        if (trimmed.Length == 0)
        {
            return (string.Empty, []);
        }

        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        foreach (var ch in trimmed)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(ch);
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        if (tokens.Count == 0)
        {
            return (string.Empty, []);
        }

        var path = tokens[0];
        var oshArgs = tokens.Skip(1).ToArray();
        return (path, oshArgs);
    }
    private static string[] ParseArgList(string args)
    {
        var trimmed = args.Trim();
        if (trimmed.Length == 0)
        {
            return [];
        }

        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        foreach (var ch in trimmed)
        {
            if (ch == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (char.IsWhiteSpace(ch) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(ch);
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens.ToArray();
    }
}
