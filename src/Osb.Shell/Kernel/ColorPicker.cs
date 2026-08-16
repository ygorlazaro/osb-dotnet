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

        int foreColor;
        int backColor;
        int focusColor;
        while (true)
        {
            foreColor = AskColor(I18nService.Get("misc.fore_color_prompt"), env.Config.ForeColor);
            backColor = AskColor(I18nService.Get("misc.back_color_prompt"), env.Config.BackColor);
            focusColor = AskColor(I18nService.Get("misc.focus_color_prompt"), env.Config.FocusColor);

            if (foreColor != backColor && foreColor != focusColor && backColor != focusColor)
            {
                break;
            }

            Console.WriteLine();
            Console.WriteLine(I18nService.Get("fs.same_colors"));
            Console.WriteLine(I18nService.Get("fs.choose_different_colors"));
            Console.WriteLine();
        }

        env.Config.ForeColor = foreColor;
        env.Config.BackColor = backColor;
        env.Config.FocusColor = focusColor;
        env.Config.Save(env.ConfigFile);
        env.ApplyColors();
        Console.Clear();
    }

    private static int AskColor(string prompt, int current)
    {
        while (true)
        {
            Console.Write($"{prompt} ({current}): ");
            var input = Console.ReadLine() ?? "";
            if (string.IsNullOrWhiteSpace(input))
            {
                return current;
            }

            if (int.TryParse(input, out var value) && value is >= 0 and < 16)
            {
                return value;
            }

            Console.WriteLine(I18nService.Get("fs.invalid_color"));
        }
    }
}