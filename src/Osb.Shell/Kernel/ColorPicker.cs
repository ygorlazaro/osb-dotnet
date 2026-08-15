namespace Osb.Shell.Kernel;

public static class ColorPicker
{
    public static void Run(OsbEnvironment env)
    {
        Console.Clear();
        Console.WriteLine(I18nService.Get("misc.color_table"));
        Console.WriteLine();
        for (var i = 0; i < DosColors.Names.Length; i++)
            Console.WriteLine($"{i,2} - {DosColors.Names[i]}");
        Console.WriteLine();

        int letra;
        int fundo;
        while (true)
        {
            letra = AskColor(I18nService.Get("misc.fore_color_prompt"), env.Config.ForeColor);
            fundo = AskColor(I18nService.Get("misc.back_color_prompt"), env.Config.BackColor);

            if (letra != fundo)
            {
                break;
            }

            Console.WriteLine();
            Console.WriteLine(I18nService.Get("fs.same_colors"));
            Console.WriteLine(I18nService.Get("fs.choose_different_colors"));
            Console.WriteLine();
        }

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
            {
                return value;
            }

            Console.WriteLine(I18nService.Get("fs.invalid_color"));
        }
    }
}