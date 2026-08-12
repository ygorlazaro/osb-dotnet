namespace Osb.Shell.Kernel;

public static class About
{
    private static readonly string[] Lines =
    [
        "****************************************",
        "******                            ******",
        "******       OSB Versão 0.2       ******",
        "******   Operating System Basic   ******",
        "****** Sistema Operacional Básico ******",
        "******                            ******",
        "******         CRÉDITOS           ******",
        "******                            ******",
        "******      www.osb.rg3.net       ******",
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
        Console.WriteLine("Para mais informações sobre o OSB, o projeto OSB Brasil, sobre como obter e");
        Console.WriteLine("ajudar o projeto, entre em contato conosco.");
        Console.WriteLine();
        Console.WriteLine("Site original: http://www.osb.rg3.net");
        Console.WriteLine("Criado por: Ygor Lazaro, entre os 14 e 16 anos, em BASIC (BC7)");
        Console.WriteLine("Este porte: .NET 10, feito ~30 anos depois.");
    }
}
