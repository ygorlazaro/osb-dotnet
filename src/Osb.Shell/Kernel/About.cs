namespace Osb.Shell.Kernel;

public static class About
{
    private static readonly string[] Lines =
    [
        "****************************************",
        "******                            ******",
        "******         OSB 3.0           ******",
        "******   Operating System Basic   ******",
        "****** Sistema Operacional Básico ******",
        "******                            ******",
        "******       Feito por Ygor Lazaro      ******",
        "******                            ******",
        "****************************************"
    ];

    public static void Show()
    {
        Console.Clear();
        foreach (var line in Lines)
        {
            var pad = Math.Max(0, (Console.IsOutputRedirected ? 40 : 40) - line.Length) / 2;
            Console.WriteLine(new string(' ', pad) + line);
        }

        Console.WriteLine();
        Console.WriteLine(I18nService.Get("misc.about_hint"));
        Console.WriteLine();
    }
}
