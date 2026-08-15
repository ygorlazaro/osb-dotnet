using System.Diagnostics;

namespace Osb.Shell.Kernel;

/// <summary>
/// Launches Osb.Xwin as a separate process that takes over the terminal.
/// OSB waits for Xwin to exit, then returns to the prompt.
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
