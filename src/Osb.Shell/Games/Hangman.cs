namespace Osb.Shell.Games;

/// <summary>
/// Porte jogável do jogo da forca (HANGMAN.BAS / HANGMAN.EXE do OSB original).
/// </summary>
public static class Hangman
{
    private static readonly string[] Words =
    {
        "COMPUTADOR", "TECLADO", "MONITOR", "PROGRAMA", "BASIC",
        "SISTEMA", "MEMORIA", "ARQUIVO", "DIRETORIO", "KERNEL"
    };

    private static readonly string[] Stages =
    {
        "\n\n\n\n\n=========",
        "\n  |\n  |\n  |\n  |\n  |\n=========",
        "  +---+\n  |\n  |\n  |\n  |\n  |\n=========",
        "  +---+\n  |   |\n  |\n  |\n  |\n  |\n=========",
        "  +---+\n  |   |\n  |   O\n  |\n  |\n  |\n=========",
        "  +---+\n  |   |\n  |   O\n  |   |\n  |\n  |\n=========",
        "  +---+\n  |   |\n  |   O\n  |  /|\\\n  |\n  |\n=========",
        "  +---+\n  |   |\n  |   O\n  |  /|\\\n  |  / \\\n  |\n=========",
    };

    public static void Play()
    {
        var rnd = new Random();
        var word = Words[rnd.Next(Words.Length)];
        var guessed = new HashSet<char>();
        int errors = 0;
        int maxErrors = Stages.Length - 1;

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

            var display = string.Concat(word.Select(c => guessed.Contains(c) ? c : '_'));
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
            if (input == "0" || input.Length == 0) return;

            var letter = char.ToUpperInvariant(input[0]);
            if (!char.IsLetter(letter) || guessed.Contains(letter)) continue;

            guessed.Add(letter);
            if (!word.Contains(letter)) errors++;
        }

        Console.Clear();
        Console.WriteLine("*** JOGO DA FORCA ***\n");
        Console.WriteLine(Stages[errors]);
        Console.WriteLine($"\nVocê perdeu! A palavra era: {word}");
    }
}
