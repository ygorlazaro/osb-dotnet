using System.Diagnostics;
using System.Linq;
using Osb.Shell.Apps;
using Osb.Shell.Games;

namespace Osb.Shell.Kernel;

public partial class OsbShell
{
    public void Execute(string rawInput)
    {
        var raw = rawInput.Trim();
        var command = raw.ToUpperInvariant();
        if (command == "RPT") { raw = _lastRaw; command = _lastCommand; }
        if (command == "")
        {
            return;
        }

        _lastCommand = command;
        _lastRaw = raw;

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

        switch (verb)
        {
            case "ABOUT": About.Show(); break;
            case "APLIC": RunAplic(args); break;
            case "CAL": Calendar.Show(args); break;
            case "CD": ChangeDirectory(args); break;
            case "CLS": case "CLEAR": Console.Clear(); break;
            case "COLOR": ColorPicker.Run(_env); break;
            case "CONFIG": ConfigUtility.Run(_env); break;
            case "COPY": CopyFile(); break;
            case "DATE":
                Console.WriteLine("Data atual: " + DateTime.Now.ToString("dd/MM/yyyy"));
                Console.WriteLine("(Alterar a data do sistema não é suportado nesta versão portada.)");
                break;
            case "DIR": ListDirectory(args); break;
            case "PWD": Console.WriteLine(Directory.GetCurrentDirectory()); break;
            case "ERASE": EraseFiles(args); break;
            case "EXIT": DoExit(); break;
            case "GAMES": RunGames(args); break;
            case "HELP": HelpTexts.Show(args); break;
            case "HISTORY": RunHistory(args); break;
            case "HOSTNAME": HandleHostname(args); break;
            case "KISS": TextEditor.Run(args, _env); break;
            case "MD": MakeDirectory(args); break;
            case "PRINT": PrintFile(args); break;
            case "RD": RemoveDirectory(args); break;
            case "REN": RenameFile(); break;
            case "SIZE": ShowSize(args); break;
            case "USER": HandleUser(args); break;
            case "TIME":
                Console.WriteLine("Hora atual: " + DateTime.Now.ToString("HH:mm:ss"));
                Console.WriteLine("(Alterar a hora do sistema não é suportado nesta versão portada.)");
                break;
            case "TREE": ShowTree(Directory.GetCurrentDirectory(), ""); break;
            case "TYPE": TypeFile(args); break;
            case "VER":
                Console.WriteLine("OSB Versão 0.2 (porte para .NET 10)");
                Console.WriteLine("Original: http://www.osb.rg3.net");
                Console.WriteLine();
                Console.WriteLine("Digite ABOUT para mais informações");
                break;
            case "X":
                XwinLauncher.Launch();
                _env.ApplyColors();
                break;
            default:
                RunExternal(raw);
                break;
        }
    }

    private void RunExternal(string cmd)
    {
        cmd = cmd.Trim();
        if (cmd == "")
        {
            return;
        }

        try
        {
            Console.WriteLine("Executando um programa externo (fora do kernel).\n");
            var psi = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
                Arguments = OperatingSystem.IsWindows() ? $"/c {cmd}" : $"-c \"{cmd}\"",
                UseShellExecute = false
            };
            using var p = Process.Start(psi);
            p?.WaitForExit();
            Console.WriteLine("Final da execução");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Não foi possível executar: " + ex.Message);
        }
        finally
        {
            _env.ApplyColors();
        }
    }
}
