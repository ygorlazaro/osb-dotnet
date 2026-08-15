using System.Globalization;
using Osb.Shell.Kernel;

namespace Osb.Shell.Apps;

public static class Calendar
{
    private static readonly CultureInfo Culture = new("pt-BR");

    public static void Show(string args)
    {
        var now = DateTime.Now;
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        switch (parts.Length)
        {
            case 0:
                ShowMonth(now.Month, now.Year, now);
                return;
            case 1:
            {
                if (!int.TryParse(parts[0], out var value))
                {
                    Console.WriteLine(I18nService.Get("calendar.usage"));
                    return;
                }

                if (value is >= 1 and <= 12)
                {
                    ShowMonth(value, now.Year, now);
                }
                else
                {
                    ShowYear(value, now);
                }

                return;
            }
        }

        if (!int.TryParse(parts[0], out var month) || !int.TryParse(parts[1], out var year) || month is < 1 or > 12)
        {
            Console.WriteLine(I18nService.Get("calendar.usage"));
            return;
        }

        ShowMonth(month, year, now);
    }

    private static void ShowYear(int year, DateTime now)
    {
        Console.WriteLine();
        Console.WriteLine($"=== {year} ===");
        for (var month = 1; month <= 12; month++)
            ShowMonth(month, year, now);
    }

    private static void ShowMonth(int month, int year, DateTime now)
    {
        var monthNames = new[]
        {
            I18nService.Get("calendar.month_january"),
            I18nService.Get("calendar.month_february"),
            I18nService.Get("calendar.month_march"),
            I18nService.Get("calendar.month_april"),
            I18nService.Get("calendar.month_may"),
            I18nService.Get("calendar.month_june"),
            I18nService.Get("calendar.month_july"),
            I18nService.Get("calendar.month_august"),
            I18nService.Get("calendar.month_september"),
            I18nService.Get("calendar.month_october"),
            I18nService.Get("calendar.month_november"),
            I18nService.Get("calendar.month_december")
        };

        var weekDayNames = new[]
        {
            I18nService.Get("calendar.weekday_sunday"),
            I18nService.Get("calendar.weekday_monday"),
            I18nService.Get("calendar.weekday_tuesday"),
            I18nService.Get("calendar.weekday_wednesday"),
            I18nService.Get("calendar.weekday_thursday"),
            I18nService.Get("calendar.weekday_friday"),
            I18nService.Get("calendar.weekday_saturday")
        };

        Console.WriteLine();
        Console.WriteLine($"   {monthNames[month - 1].ToUpperInvariant()} / {year}");
        Console.WriteLine(string.Join(" ", weekDayNames));

        var first = new DateTime(year, month, 1);
        var startPad = (int)first.DayOfWeek;
        var daysInMonth = DateTime.DaysInMonth(year, month);

        Console.Write(new string(' ', startPad * 4));
        for (var day = 1; day <= daysInMonth; day++)
        {
            var isToday = day == now.Day && month == now.Month && year == now.Year;
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

            if ((day + startPad) % 7 == 0)
            {
                Console.WriteLine();
            }
        }
        Console.WriteLine();

        if (month == now.Month && year == now.Year)
        {
            Console.WriteLine(I18nService.Get("calendar.today", now.ToString("dddd, dd/MM/yyyy HH:mm:ss", Culture)));
        }
    }
}
