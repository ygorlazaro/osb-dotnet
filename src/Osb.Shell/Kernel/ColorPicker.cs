namespace Osb.Shell.Kernel;

/// <summary>Porte do COLOR.BAS: tabela de cores + escolha de cor de letra/fundo.</summary>
public static class ColorPicker
{
    public static void Run(OsbEnvironment env)
    {
        Console.Clear();
        Console.WriteLine("Tabela de cores:");
        Console.WriteLine();
        for (int i = 0; i < DosColors.Names.Length; i++)
            Console.WriteLine($"{i,2} - {DosColors.Names[i]}");
        Console.WriteLine();

        int letra = AskColor("Entre com a cor das letras: ", env.Config.ForeColor);
        int fundo = AskColor("Entre com a cor do fundo: ", env.Config.BackColor);

        env.Config.ForeColor = letra;
        env.Config.BackColor = fundo;
        env.Config.Save(env.ConfigFile);
        env.ApplyColors();
        Console.Clear();
    }

    private static int AskColor(string prompt, int current)
    {
        while (true)
        {
            Console.Write(prompt);
            var input = Console.ReadLine() ?? "";
            if (int.TryParse(input, out var value) && value is >= 0 and < 16)
                return value;
            Console.WriteLine("Valor inválido, deve ser de 0 a 15.");
        }
    }
}
