using System.Diagnostics;
using System.Linq;
using Osb.Shell.Apps;

namespace Osb.Shell.Kernel;

public partial class OsbShell
{
    public void Execute(string rawInput, bool requireAuth = true)
    {
        if (rawInput.Contains(';'))
        {
            ExecutePipeline(rawInput, requireAuth);
            return;
        }

        var raw = rawInput.Trim();
        var command = raw.ToUpperInvariant();

        if (command == "")
        {
            return;
        }

        var spaceIndex = raw.IndexOf(' ');
        var verb = spaceIndex < 0 ? command : command[..spaceIndex];

        if (requireAuth && !_isAuthenticated && verb != "USER" && verb != "HOSTNAME")
        {
            var cmdPart = raw.Contains(' ') ? raw[..raw.IndexOf(' ')] : raw;
            var isOsh = cmdPart.EndsWith(".osh", StringComparison.OrdinalIgnoreCase) && File.Exists(cmdPart);
            if (!isOsh)
            {
                Console.WriteLine("Você deve entrar com login. Use USER para autenticar.");
                return;
            }
        }

        if (command.Length == 2 && command[1] == ':')
        {
            Console.WriteLine("Conceito de drive não se aplica neste sistema operacional.");
            return;
        }

        if (command.EndsWith("/?"))
        {
            if (verb == "OSL")
            {
                RunOslHelp();
            }
            else
            {
                HelpTexts.Show(raw[..^2].TrimEnd());
            }
            return;
        }

        var args = spaceIndex < 0 ? "" : raw[(spaceIndex + 1)..].Trim();
        args = ExpandVariables(args);

        var handled = false;
        switch (verb)
        {
            case "ABOUT":
                About.Show(); handled = true; break;
            case "APLIC":
                RunAplic(args); handled = true; break;
            case "CAL":
                Calendar.Show(args); handled = true; break;
            case "CD":
                ChangeDirectory(args); handled = true; break;
            case "CLS":
            case "CLEAR":
                Console.Clear(); handled = true; break;
            case "COLOR":
                ColorPicker.Run(_env); handled = true; break;
            case "CONFIG":
                ConfigUtility.Run(_env); handled = true; break;
            case "COPY":
                CopyFile(args); handled = true; break;
            case "DATE":
                Console.WriteLine("Data atual: " + DateTime.Now.ToString("dd/MM/yyyy"));
                Console.WriteLine("(Alterar a data do sistema não é suportado.)");
                handled = true; break;
            case "DIR":
                ListDirectory(args); handled = true; break;
            case "PWD":
                Console.WriteLine(Directory.GetCurrentDirectory()); handled = true; break;
            case "DEL":
                DeleteFiles(args); handled = true; break;
            case "EXIT":
                DoExit(); handled = true; break;
            case "GAMES":
                RunGames(args); handled = true; break;
            case "HELP":
                if (args.Equals("OSL", StringComparison.OrdinalIgnoreCase))
                {
                    RunOslHelp();
                }
                else
                {
                    HelpTexts.Show(args);
                }
                handled = true; break;
            case "HISTORY":
                RunHistory(args); handled = true; break;
            case "HOSTNAME":
                HandleHostname(args); handled = true; break;
            case "KISS":
                TextEditor.Run(args, _env); handled = true; break;
            case "MD":
                MakeDirectory(args); handled = true; break;
            case "MOVE":
                MoveFile(args); handled = true; break;
            case "OSL":
                RunOslFile(args); handled = true; break;
            case "PRINT":
                PrintFile(args); handled = true; break;
            case "PROMPT":
                SetPromptLayout(args); handled = true; break;
            case "RD":
                RemoveDirectory(args); handled = true; break;
            case "REN":
                RenameFile(args); handled = true; break;
            case "RUN":
            {
                var (path, oshArgs) = ParseOshArgs(args);
                RunOshFile(path, oshArgs);
                handled = true;
                break;
            }
            case "SET":
                SetVariable(args); handled = true; break;
            case "SIZE":
                ShowSize(args); handled = true; break;
            case "FIND":
                FindFiles(args); handled = true; break;
            case "GREP":
                ExecuteGrep(args); handled = true; break;
            case "USER":
                HandleUser(args); handled = true; break;
            case "TIME":
                Console.WriteLine("Hora atual: " + DateTime.Now.ToString("HH:mm:ss"));
                Console.WriteLine("(Alterar a hora do sistema não é suportado.)");
                handled = true; break;
            case "TYPE":
                TypeFile(args); handled = true; break;
            case "VER":
                Console.WriteLine("OSB 3.0");
                Console.WriteLine();
                Console.WriteLine("Digite ABOUT para mais informações");
                handled = true; break;
            case "X":
                XwinLauncher.Launch();
                _env.ApplyColors(); handled = true; break;
            default:
            {
                var firstSpace = raw.IndexOf(' ');
                var cmdPart = firstSpace < 0 ? raw : raw[..firstSpace];

                if (cmdPart.EndsWith(".osh", StringComparison.OrdinalIgnoreCase))
                {
                    var resolved = PathResolver.Resolve(cmdPart);
                    if (File.Exists(resolved))
                    {
                        var oshArgs = firstSpace < 0 ? [] : ParseArgList(raw[(firstSpace + 1)..].Trim());
                        RunOshFile(resolved, oshArgs);
                        handled = true;
                        break;
                    }
                }

                handled = RunExternal(raw);
                break;
            }
        }
    }

 // ... existing code ...
    private bool RunExternal(string cmd)
    {
        cmd = cmd.Trim();
        if (cmd == "")
        {
            return false;
        }

        if (!cmd.StartsWith('.'))
        {
            Console.WriteLine("Comando não reconhecido.");
            Console.WriteLine("Para executar um programa externo, use . antes do comando.");
            return false;
        }

        cmd = cmd[1..].Trim();
        if (cmd == "")
        {
            Console.WriteLine("Uso: . <comando externo>");
            return false;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
                Arguments = OperatingSystem.IsWindows() ? $"/c {cmd}" : $"-c \"{cmd}\"",
                UseShellExecute = false
            };

            var prevTreat = Console.TreatControlCAsInput;
            ConsoleCancelEventHandler? handler = null;
            using var p = Process.Start(psi);
            try
            {
                if (p == null)
                {
                    Console.WriteLine("Não foi possível iniciar o processo solicitado.");
                    return false;
                }

                Console.TreatControlCAsInput = false;
                handler = (s, e) =>
                {
                    e.Cancel = true;
                    try { if (!p.HasExited) p.Kill(); } catch { }
                };
                Console.CancelKeyPress += handler;
                p.WaitForExit();

                return true;
            }
            finally
            {
                if (handler != null) Console.CancelKeyPress -= handler;
                Console.TreatControlCAsInput = prevTreat;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Não foi possível executar: " + ex.Message);
            return false;
        }
        finally
        {
            _env.ApplyColors();
        }
    }

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
