using System.Globalization;

namespace Osb.Shell.Apps;

/// <summary>
/// Porte do aplicativo CAL.COM (calendário em tempo real) do OSB original.
///
/// Uso:
///   CAL            - mês atual
///   CAL 5          - maio do ano atual
///   CAL 5 2015     - maio de 2015
///   CAL 2015       - o ano de 2015 inteiro (um único número fora do intervalo 1-12
///                    é tratado como ano, não como mês - esse era o bug reportado:
///                    "CAL 2015" caía no ramo de mês inválido e mostrava o mês atual
///                    em vez do ano pedido).
/// </summary>
public static class Calendar
{
    private static readonly CultureInfo Culture = new("pt-BR");

    public static void Show(string args)
    {
        var now = DateTime.Now;
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            ShowMonth(now.Month, now.Year, now);
            return;
        }

        if (parts.Length == 1)
        {
            if (!int.TryParse(parts[0], out var value))
            {
                Console.WriteLine("Uso: CAL [mês] [ano]  ou  CAL <ano>");
                return;
            }

            if (value is >= 1 and <= 12)
                ShowMonth(value, now.Year, now);
            else
                ShowYear(value, now);
            return;
        }

        if (!int.TryParse(parts[0], out var month) || !int.TryParse(parts[1], out var year) || month is < 1 or > 12)
        {
            Console.WriteLine("Uso: CAL [mês] [ano]  ou  CAL <ano>");
            return;
        }

        ShowMonth(month, year, now);
    }

    private static void ShowYear(int year, DateTime now)
    {
        Console.WriteLine();
        Console.WriteLine($"=== {year} ===");
        for (int month = 1; month <= 12; month++)
            ShowMonth(month, year, now);
    }

    private static void ShowMonth(int month, int year, DateTime now)
    {
        Console.WriteLine();
        Console.WriteLine($"   {Culture.DateTimeFormat.GetMonthName(month).ToUpperInvariant()} / {year}");
        Console.WriteLine("Dom Seg Ter Qua Qui Sex Sáb");

        var first = new DateTime(year, month, 1);
        int startPad = (int)first.DayOfWeek;
        int daysInMonth = DateTime.DaysInMonth(year, month);

        Console.Write(new string(' ', startPad * 4));
        for (int day = 1; day <= daysInMonth; day++)
        {
            bool isToday = day == now.Day && month == now.Month && year == now.Year;
            var text = day.ToString().PadLeft(2);
            if (isToday)
            {
                var fg = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"[{text}]");
                Console.ForegroundColor = fg;
            }
            else
            {
                Console.Write($" {text} ");
            }

            if ((day + startPad) % 7 == 0) Console.WriteLine();
        }
        Console.WriteLine();

        if (month == now.Month && year == now.Year)
            Console.WriteLine($"Hoje: {now.ToString("dddd, dd/MM/yyyy HH:mm:ss", Culture)}");
    }
}
