using System.Globalization;

namespace Osb.Shell.Apps;

public static class Tour
{
    private static void Pause()
    {
        Console.WriteLine();
        Console.Write("-----Pressione ENTER para continuar----");
        Console.ReadLine();
    }

    private static void Step(int number, string title, string body)
    {
        Console.WriteLine();
        Console.WriteLine($"=== Passo {number}: {title} ===");
        Console.WriteLine();
        Console.WriteLine(body);
        Pause();
    }

    public static void Show(string args)
    {
        var user = string.IsNullOrWhiteSpace(args) ? "você" : args.Trim();

        Console.WriteLine($"Bem-vindo ao Tour do OSB, {user}!");
        Console.WriteLine("Vamos aprender a usar o sistema passo a passo.");
        Pause();

        Step(1, "O que é o OSB",
            "O OSB é um sistema operacional em texto, inspirado no DOS e no BASIC.\n" +
            "Você interage com ele digitando comandos e vendo o resultado na tela.\n" +
            "Todos os comandos são case-insensitive: DIR, dir e Dir funcionam igual.");

        Step(2, "Navegação de pastas",
            "Use CD para mudar de pasta e DIR para listar arquivos.\n" +
            "Exemplos:\n" +
            "  DIR               → lista arquivos da pasta atual\n" +
            "  DIR /W            → lista em colunas\n" +
            "  CD TEMP           → entra na pasta TEMP\n" +
            "  CD ..             → volta para a pasta anterior\n" +
            "  PWD               → mostra a pasta atual");

        Step(3, "Arquivos e pastas",
            "Use MD para criar pastas e COPY para copiar arquivos.\n" +
            "Exemplos:\n" +
            "  MD MEUDIR         → cria uma pasta chamada MEUDIR\n" +
            "  MD /P A\\B\\C      → cria pastas pais automaticamente\n" +
            "  COPY A.TXT B.TXT  → copia A.TXT para B.TXT\n" +
            "  DEL ARQ.TXT       → apaga um arquivo\n" +
            "  REN ANTIGO.TXT NOVO.TXT → renomeia");

        Step(4, "Vendo o conteúdo de arquivos",
            "Use TYPE para exibir arquivos de texto na tela.\n" +
            "Exemplos:\n" +
            "  TYPE ARQ.TXT      → exibe o conteúdo do arquivo\n" +
            "  TYPE /N ARQ.TXT   → exibe com números de linha\n" +
            "  TYPE /P ARQ.TXT   → exibe com pausa a cada 20 linhas\n" +
            "  FIND texto        → busca texto dentro de arquivos");

        Step(5, "Variáveis",
            "Use SET para criar variáveis que podem ser usadas em comandos.\n" +
            "Exemplos:\n" +
            "  SET NOME=Ygor     → define a variável NOME\n" +
            "  SET NOME          → mostra o valor da variável\n" +
            "  SET NOME=         → remove a variável\n" +
            "  SET               → lista todas as variáveis\n" +
            "Variáveis são expandidas automaticamente nos comandos usando %NOME%.");

        Step(6, "Calculadora rápida",
            "Use PRINT para avaliar expressões matemáticas.\n" +
            "Exemplos:\n" +
            "  PRINT 2+2         → exibe 4\n" +
            "  PRINT 10*5+3      → exibe 53\n" +
            "  PRINT (2+2)*3     → exibe 12\n" +
            "Operadores suportados: +, -, *, / e parênteses.");

        Step(7, "Pipelines e filtros",
            "Use ; para encadear comandos (pipe) e GREP para filtrar linhas.\n" +
            "Exemplos:\n" +
            "  DIR ; GREP git    → lista apenas itens que contém 'git'\n" +
            "  HISTORY ; GREP CD → filtra o histórico por comandos com CD\n" +
            "  TYPE ARQ.TXT ; GREP erro → mostra linhas com 'erro'");

        Step(8, "Aplicativos e ajuda",
            "Use APLIC para ver e executar aplicativos instalados.\n" +
            "Use HELP para ver a lista de todos os comandos.\n" +
            "Use HELP <comando> ou <comando> /? para ver ajuda específica.\n" +
            "Exemplos:\n" +
            "  APLIC             → lista aplicativos\n" +
            "  APLIC CAL         → abre o calendário\n" +
            "  HELP DIR          → ajuda do comando DIR\n" +
            "  DIR /?            → mesma coisa");

        Console.WriteLine();
        Console.WriteLine("=== Fim do Tour ===");
        Console.WriteLine("Agora você já sabe o básico para usar o OSB!");
        Console.WriteLine("Use HELP para explorar mais comandos.");
        Console.WriteLine();
    }
}
