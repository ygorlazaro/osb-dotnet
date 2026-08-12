using System.Diagnostics;

namespace Osb.Shell.Kernel;

/// <summary>
/// Localiza e executa o Osb.Xwin como um processo separado que assume o terminal
/// (igual o COMMAND.COM do MS-DOS 6.22 chamava o WIN.COM): o OSB "congela" no
/// Process.WaitForExit, o XWIN tem a tela toda para ele, e quando ele termina o
/// controle volta pro prompt do OSB, exatamente de onde parou.
/// </summary>
public static class XwinLauncher
{
    public static void Launch()
    {
        var dllPath = FindXwinDll();
        if (dllPath is null)
        {
            Console.WriteLine("Não encontrei o XWIN.");
            Console.WriteLine("Rode 'dotnet build' em src/Osb.Xwin primeiro, ou defina a");
            Console.WriteLine("variável de ambiente OSB_XWIN_PATH apontando para o Osb.Xwin.dll.");
            return;
        }

        try
        {
            Console.WriteLine("Carregando o XWIN...");
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{dllPath}\"",
                UseShellExecute = false, // herda o console atual (stdin/stdout/stderr) direto
            };
            using var process = Process.Start(psi);
            process?.WaitForExit();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Não foi possível iniciar o XWIN: " + ex.Message);
        }
    }

    private static string? FindXwinDll()
    {
        // 1) Variável de ambiente tem prioridade (útil se o XWIN foi publicado em outro lugar).
        var envPath = Environment.GetEnvironmentVariable("OSB_XWIN_PATH");
        if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
        {
            return envPath;
        }

        // 2) Layout padrão do repositório: src/Osb.Shell e src/Osb.Xwin são projetos irmãos.
        //    A partir de AppContext.BaseDirectory (.../src/Osb.Shell/bin/<config>/<tfm>/),
        //    subimos até a pasta "src" e procuramos o Osb.Xwin ao lado. Não fixamos o TFM
        //    (net10.0 etc.) no caminho, pra não quebrar de novo na próxima atualização de versão.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        var srcDir = dir;
        while (srcDir is not null && srcDir.Name != "src")
            srcDir = srcDir.Parent;

        if (srcDir is null) return null;
        return (from config in new[] { "Debug", "Release" }
                select Path.Combine(srcDir.FullName, "Osb.Xwin", "bin", config)
                into binDir
                where Directory.Exists(binDir)
                select Directory.GetFiles(binDir, "Osb.Xwin.dll", SearchOption.AllDirectories)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault()).OfType<string>()
            .FirstOrDefault();
    }
}
