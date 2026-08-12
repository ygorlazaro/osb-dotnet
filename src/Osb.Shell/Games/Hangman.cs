namespace Osb.Shell.Games;

public static class Hangman
{
    private static readonly string[] Words = LoadWords();

    private static readonly string[] Stages =
    [
        "\n\n\n\n\n=========",
        "\n  |\n  |\n  |\n  |\n  |\n=========",
        "  +---+\n  |\n  |\n  |\n  |\n  |\n=========",
        "  +---+\n  |   |\n  |\n  |\n  |\n  |\n=========",
        "  +---+\n  |   |\n  |   O\n  |\n  |\n  |\n=========",
        "  +---+\n  |   |\n  |   O\n  |   |\n  |\n  |\n=========",
        "  +---+\n  |   |\n  |   O\n  |  /|\\\n  |\n  |\n=========",
        "  +---+\n  |   |\n  |   O\n  |  /|\\\n  |  / \\\n  |\n========="
    ];

    public static void Play()
    {
        if (Words.Length == 0)
        {
            Console.WriteLine("Nenhuma palavra disponível para o jogo HANGMAN. Verifique CONF/HANGMAN.WDS.");
            return;
        }

        var rnd = new Random();
        var word = Words[rnd.Next(Words.Length)];
        var normalizedWord = NormalizeText(word);
        var guessed = new HashSet<char>();
        var errors = 0;
        var maxErrors = Stages.Length - 1;

        Console.WriteLine();
        Console.WriteLine("*** JOGO DA FORCA ***");
        Console.WriteLine("Tente adivinhar a palavra letra por letra.");
        Console.WriteLine();

        while (errors < maxErrors)
        {
            Console.Clear();
            Console.WriteLine("*** JOGO DA FORCA ***\n");
            Console.WriteLine(Stages[errors]);
            Console.WriteLine();

            var display = string.Concat(word.Select((c, i) => guessed.Contains(normalizedWord[i]) ? c : '_'));
            Console.WriteLine("Palavra: " + string.Join(' ', display.ToCharArray()));
            Console.WriteLine($"Erros: {errors}/{maxErrors}");
            Console.WriteLine("Letras já tentadas: " + string.Join(", ", guessed.OrderBy(c => c)));

            if (!display.Contains('_'))
            {
                Console.WriteLine("\nVocê ganhou! A palavra era: " + word);
                return;
            }

            Console.Write("\nDigite uma letra (ou 0 para sair): ");
            var input = (Console.ReadLine() ?? "").Trim();
            if (input == "0" || input.Length == 0)
            {
                return;
            }

            var letter = NormalizeChar(char.ToUpperInvariant(input[0]));
            if (!char.IsLetter(letter) || guessed.Contains(letter))
            {
                continue;
            }

            guessed.Add(letter);
            if (!normalizedWord.Contains(letter))
            {
                errors++;
            }
        }

        Console.Clear();
        Console.WriteLine("*** JOGO DA FORCA ***\n");
        Console.WriteLine(Stages[errors]);
        Console.WriteLine($"\nVocê perdeu! A palavra era: {word}");
    }

    private static string[] LoadWords()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "CONF", "HANGMAN.WDS");
            if (!File.Exists(path))
                return Array.Empty<string>();

            return File.ReadAllLines(path)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static string NormalizeText(string text)
    {
        var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
        var builder = new System.Text.StringBuilder();

        foreach (var ch in normalized)
        {
            var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }

        return builder.ToString().Normalize(System.Text.NormalizationForm.FormC).Replace('Ç', 'C').Replace('ç', 'c');
    }

    private static char NormalizeChar(char ch)
    {
        var normalized = NormalizeText(ch.ToString());
        return normalized.Length > 0 ? normalized[0] : ch;
    }
}
