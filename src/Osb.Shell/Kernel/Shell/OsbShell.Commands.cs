using System.Diagnostics;
using System.Linq;
using Osb.Shell.Apps;
using Osb.Shell.Games;

namespace Osb.Shell.Kernel;

public partial class OsbShell
{
    public void Execute(string rawInput)
    {
        // support multiple commands separated by ';' on the same line
        if (rawInput.Contains(';'))
        {
            var parts = rawInput.Split(';');
            foreach (var part in parts)
            {
                var p = part.Trim();
                if (p.Length == 0) continue;
                Execute(p);
            }
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

        if (!_isAuthenticated && verb != "USER" && verb != "HOSTNAME")
        {
            Console.WriteLine("Você deve entrar com login. Use USER para autenticar.");
            return;
        }

        if (command.Length == 2 && command[1] == ':')
        {
            Console.WriteLine("Conceito de drive não se aplica neste sistema operacional.");
            return;
        }

        if (command.EndsWith("/?"))
        {
            HelpTexts.Show(raw[..^2].TrimEnd());
            return;
        }

        var args = spaceIndex < 0 ? "" : raw[(spaceIndex + 1)..].Trim();

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
                Console.WriteLine("(Alterar a data do sistema não é suportado nesta versão portada.)");
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
                HelpTexts.Show(args); handled = true; break;
            case "HISTORY":
                RunHistory(args); handled = true; break;
            case "HOSTNAME":
                HandleHostname(args); handled = true; break;
            case "KISS":
                TextEditor.Run(args, _env); handled = true; break;
            case "MD":
                MakeDirectory(args); handled = true; break;
            case "OSL":
                RunOslFile(args); handled = true; break;
            case "PRINT":
                PrintFile(args); handled = true; break;
            case "RD":
                RemoveDirectory(args); handled = true; break;
            case "REN":
                RenameFile(); handled = true; break;
            case "SIZE":
                ShowSize(args); handled = true; break;
            case "USER":
                HandleUser(args); handled = true; break;
            case "TIME":
                Console.WriteLine("Hora atual: " + DateTime.Now.ToString("HH:mm:ss"));
                Console.WriteLine("(Alterar a hora do sistema não é suportado nesta versão portada.)");
                handled = true; break;
            case "TYPE":
                TypeFile(args); handled = true; break;
            case "VER":
                Console.WriteLine("OSB 3.0 Lince (porte para .NET 10)");
                Console.WriteLine("Original: http://www.osb.rg3.net");
                Console.WriteLine();
                Console.WriteLine("Digite ABOUT para mais informações");
                handled = true; break;
            case "X":
                XwinLauncher.Launch();
                _env.ApplyColors(); handled = true; break;
            default:
                handled = RunExternal(raw);
                break;
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
}
