using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Osb.Xwin.TextMode;

namespace Osb.Xwin;

public static class MainMenu
{
    // Mesma pasta que o Osb.Shell usa (~/.osb) - não a pasta de build deste executável,
    // senão XWIN e Shell cada um enxergaria um OSB.CFG/CONF diferente e as cores/config
    // configuradas em um não apareceriam no outro.
    private static readonly string OsbHomeDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".osb");

    public static void Run()
    {
        MouseInput.Enable();
        try
        {
            new Desktop().Run();
        }
        finally
        {
            Console.CursorVisible = true;
            Console.ResetColor();
            Console.Clear();
            MouseInput.Disable();
        }
    }

    private sealed class Desktop
    {
        private readonly List<AppButton> _buttons;
        private readonly List<AppWindow> _openWindows = [];
        private AppWindow? _draggingWindow;
        private int _dragOffsetX;
        private int _dragOffsetY;
        private bool _running = true;
        private int _currentForeColor;
        private int _currentBackColor;
        private ConsoleColor _desktopForeground;
        private ConsoleColor _desktopBackground;
        private ConsoleColor _panelForeground;
        private ConsoleColor _panelBackground;
        private int _topReopenStart;
        private int _topReopenEnd;
        private int _topExitStart;
        private int _topExitEnd;
        private string? _lastClosedAppName;

        public Desktop()
        {
            var scheme = LoadColorScheme();
            ApplyColorScheme(scheme.ForeIndex, scheme.BackIndex);

            var config = LoadConfigEntries();
            _buttons = config.Select(entry => new AppButton(entry.Name, entry.Description, entry.Shortcut)).ToList();
            _buttons.Add(new AppButton("CONTROL", "Painel de Controle", 'P'));
            _buttons.Add(new AppButton("CALC", "Calculadora ASCII", 'C'));
            _buttons.Add(new AppButton("ABOUT", "Sobre o XWIN", 'A'));
            _openWindows.Add(new AppMenuWindow(4, 4, Math.Min(48, Console.WindowWidth - 8), 18, _buttons, LaunchOrToggle));
        }

        public void Run()
        {
            Console.ForegroundColor = _desktopForeground;
            Console.BackgroundColor = _desktopBackground;
            Console.Clear();
            Console.CursorVisible = false;
            RenderDesktop();
            while (_running)
            {
                var ev = MouseInput.Read();
                if (HandleInput(ev))
                {
                    RenderDesktop();
                }
            }
        }

        private void RenderDesktop()
        {
            var width = Math.Max(1, Console.WindowWidth);
            var height = Math.Max(1, Console.WindowHeight);
            for (var row = 0; row < height; row++)
            {
                Console.SetCursorPosition(0, row);
                Console.Write(new string(' ', width));
            }

            Console.SetCursorPosition(0, 0);
            DrawDesktop();
        }

        private static IReadOnlyList<ConfigEntry> LoadConfigEntries()
        {
            var path = Path.Combine(OsbHomeDir, "CONF", "XWIN.CFG");
            if (!File.Exists(path))
            {
                return [];
            }

            var lines = File.ReadAllLines(path);
            var entries = new List<ConfigEntry>();
            for (var i = 0; i < lines.Length; i++)
            {
                var rawLine = lines[i].Trim();
                if (string.IsNullOrEmpty(rawLine) || rawLine.StartsWith(";") || rawLine.StartsWith("-"))
                {
                    continue;
                }

                var name = rawLine.ToUpperInvariant();
                var description = string.Empty;
                if (i + 1 < lines.Length)
                {
                    var candidate = lines[i + 1].Trim();
                    if (!string.IsNullOrEmpty(candidate) && !candidate.StartsWith(";") && !candidate.StartsWith("-"))
                    {
                        description = candidate;
                        i++;
                    }
                }

                var shortcut = char.IsLetterOrDigit(name[0]) ? char.ToUpperInvariant(name[0]) : '?';
                entries.Add(new ConfigEntry(name, description, shortcut));
            }
            return entries;
        }

        private static (ConsoleColor Foreground, ConsoleColor Background, int ForeIndex, int BackIndex) LoadColorScheme()
        {
            var path = Path.Combine(OsbHomeDir, "OSB.CFG");
            if (!File.Exists(path))
            {
                return (ConsoleColor.Gray, ConsoleColor.Black, 15, 1);
            }

            var lines = File.ReadAllLines(path);
            var fore = 15;
            var back = 1;
            for (var i = 0; i < lines.Length; i++)
            {
                var raw = lines[i].Trim();
                if (string.IsNullOrEmpty(raw) || raw.StartsWith(";"))
                {
                    continue;
                }

                if (raw.Equals("[FORECOLOR]", StringComparison.OrdinalIgnoreCase))
                {
                    fore = ParseConfigColor(lines, ref i, fore);
                }
                else if (raw.Equals("[BACKCOLOR]", StringComparison.OrdinalIgnoreCase))
                {
                    back = ParseConfigColor(lines, ref i, back);
                }
            }

            return (MapDosColor(fore), MapDosColor(back), fore, back);
        }

        private static int ParseConfigColor(string[] lines, ref int index, int defaultValue)
        {
            while (++index < lines.Length)
            {
                var raw = lines[index].Trim();
                if (string.IsNullOrEmpty(raw) || raw.StartsWith(";"))
                {
                    continue;
                }

                if (int.TryParse(raw, out var value) && value >= 0 && value < 16)
                {
                    return value;
                }

                return defaultValue;
            }
            return defaultValue;
        }

        private static ConsoleColor MapDosColor(int dosColor)
        {
            return dosColor switch
            {
                0 => ConsoleColor.Black,
                1 => ConsoleColor.DarkBlue,
                2 => ConsoleColor.DarkGreen,
                3 => ConsoleColor.DarkCyan,
                4 => ConsoleColor.DarkRed,
                5 => ConsoleColor.DarkMagenta,
                6 => ConsoleColor.DarkYellow,
                7 => ConsoleColor.Gray,
                8 => ConsoleColor.DarkGray,
                9 => ConsoleColor.Blue,
                10 => ConsoleColor.Green,
                11 => ConsoleColor.Cyan,
                12 => ConsoleColor.Red,
                13 => ConsoleColor.Magenta,
                14 => ConsoleColor.Yellow,
                15 => ConsoleColor.White,
                _ => ConsoleColor.Gray,
            };
        }

        internal static ConsoleColor ChooseContrast(ConsoleColor background)
        {
            return background switch
            {
                ConsoleColor.Black or ConsoleColor.DarkBlue or ConsoleColor.DarkGreen or ConsoleColor.DarkCyan or ConsoleColor.DarkRed or ConsoleColor.DarkMagenta or ConsoleColor.DarkYellow or ConsoleColor.Gray => ConsoleColor.White,
                _ => ConsoleColor.Black,
            };
        }

        private void ApplyColorScheme(int foreIndex, int backIndex)
        {
            _currentForeColor = foreIndex;
            _currentBackColor = backIndex;
            _desktopForeground = MapDosColor(foreIndex);
            _desktopBackground = MapDosColor(backIndex);
            _panelBackground = _desktopBackground;
            _panelForeground = ChooseContrast(_panelBackground);

            AppWindow.WindowTitleForeground = ChooseContrast(_desktopForeground);
            AppWindow.WindowTitleBackground = _desktopForeground;
            AppWindow.WindowForeground = ChooseContrast(_desktopBackground);
            AppWindow.WindowBackground = _desktopBackground;
            Console.ForegroundColor = _desktopForeground;
            Console.BackgroundColor = _desktopBackground;
        }

        internal static void SaveColorSettings(int foreIndex, int backIndex)
        {
            var path = Path.Combine(OsbHomeDir, "OSB.CFG");
            var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : [];
            WriteSection(lines, "[FORECOLOR]", foreIndex.ToString());
            WriteSection(lines, "[BACKCOLOR]", backIndex.ToString());
            File.WriteAllLines(path, lines);
        }

        internal static void WriteSection(List<string> lines, string header, string value)
        {
            var index = lines.FindIndex(line => line.Trim().Equals(header, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                lines.Add(header);
                lines.Add(value);
                return;
            }

            var target = index + 1;
            while (target < lines.Count && string.IsNullOrWhiteSpace(lines[target]))
                target++;

            if (target < lines.Count && !lines[target].Trim().StartsWith(";"))
            {
                lines[target] = value;
                return;
            }

            lines.Insert(index + 1, value);
        }

        private void DrawDesktop()
        {
            DrawBackground();
            DrawWindows();
            DrawTaskBar();
        }

        private void DrawBackground()
        {
            Console.ForegroundColor = _desktopForeground;
            Console.BackgroundColor = _desktopBackground;
            var width = Math.Max(10, Console.WindowWidth - 2);
            var title = " XWIN Desktop ";
            var menuLabels = " [Reabrir] [Sair] ";
            var timestamp = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
            var maxLeft = width - menuLabels.Length - timestamp.Length;
            var leftText = maxLeft > title.Length ? title : title[..Math.Max(0, maxLeft)];
            var spacer = new string(' ', Math.Max(0, maxLeft - leftText.Length));
            var line = "║" + leftText + spacer + menuLabels + timestamp + "║";
            var menuStart = 1 + leftText.Length + spacer.Length;
            _topReopenStart = menuStart;
            _topReopenEnd = _topReopenStart + "[Reabrir]".Length;
            _topExitStart = _topReopenEnd + 1;
            _topExitEnd = _topExitStart + "[Sair]".Length;

            Console.WriteLine("╔" + new string('═', width) + "╗");
            Console.WriteLine(line);
            Console.WriteLine("╚" + new string('═', width) + "╝");
            Console.WriteLine();
        }

        private void DrawWindows()
        {
            if (_draggingWindow is not null)
            {
                DrawGhostOutline(_draggingWindow.GhostX, _draggingWindow.GhostY, _draggingWindow.Width, _draggingWindow.Height);
            }

            foreach (var window in _openWindows)
            {
                if (!window.IsMinimized)
                {
                    window.Render();
                }
            }
        }

        private void DrawGhostOutline(int x, int y, int width, int height)
        {
            var left = Math.Max(0, Math.Min(x, Console.WindowWidth - width));
            var top = Math.Max(3, Math.Min(y, Console.WindowHeight - height - 2));

            var prevFg = Console.ForegroundColor;
            var prevBg = Console.BackgroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.BackgroundColor = _desktopBackground;

            try
            {
                Console.SetCursorPosition(left, top);
                Console.Write("┌" + new string('┈', Math.Max(0, width - 2)) + "┐");

                for (var r = 1; r < height - 1; r++)
                {
                    if (top + r < Console.WindowHeight - 2)
                    {
                        Console.SetCursorPosition(left, top + r);
                        Console.Write("┊");
                        Console.SetCursorPosition(Math.Min(Console.WindowWidth - 1, left + width - 1), top + r);
                        Console.Write("┊");
                    }
                }

                if (top + height - 1 < Console.WindowHeight - 2)
                {
                    Console.SetCursorPosition(left, top + height - 1);
                    Console.Write("└" + new string('┈', Math.Max(0, width - 2)) + "┘");
                }
            }
            catch
            {
                // Ignora posições fora dos limites do terminal
            }

            Console.ForegroundColor = prevFg;
            Console.BackgroundColor = prevBg;
        }

        // Barra de tarefas estilo Windows 3.11 (Program Manager): cada janela minimizada
        // vira um "ícone" - uma caixinha de duas linhas com o nome dentro - em vez de um
        // texto corrido numa lista. Janelas abertas (não minimizadas) não aparecem aqui,
        // igual no 3.11: só o que foi minimizado vira ícone no rodapé.
        private const int IconWidth = 10; // "┌────────┐" = 10 caracteres

        private List<AppWindow> MinimizedWindows() => _openWindows.Where(w => w.IsMinimized).ToList();

        private void DrawTaskBar()
        {
            var minimized = MinimizedWindows();
            if (minimized.Count == 0)
            {
                return;
            }

            var topRow = Console.WindowHeight - 2;
            var botRow = Console.WindowHeight - 1;
            if (topRow < 0)
            {
                return;
            }

            var previousFg = Console.ForegroundColor;
            var previousBg = Console.BackgroundColor;
            Console.ForegroundColor = _panelForeground;
            Console.BackgroundColor = _panelBackground;

            var col = 0;
            foreach (var w in minimized)
            {
                if (col + IconWidth > Console.WindowWidth)
                {
                    break;
                }

                var label = w.Title.Length > 8 ? w.Title[..8] : w.Title;
                var padded = label.PadLeft((8 + label.Length) / 2).PadRight(8);

                Console.SetCursorPosition(col, topRow);
                Console.Write("┌" + new string('─', 8) + "┐");
                Console.SetCursorPosition(col, botRow);
                Console.Write("│" + padded + "│");

                col += IconWidth + 1;
            }

            Console.ForegroundColor = previousFg;
            Console.BackgroundColor = previousBg;
        }

        private bool HandleInput(InputEvent ev)
        {
            if (ev.Key is { } key)
            {
                if (key.Key == ConsoleKey.Escape)
                {
                    // IMPORTANTE: ESC não fecha mais o XWIN inteiro. Um clique de mouse rápido
                    // às vezes chega em duas rajadas de bytes (o ESC do início da sequência SGR
                    // primeiro, o "[<0;X;YM" um instante depois); se WaitAvailable perder essa
                    // segunda rajada por uma corrida de tempo real, um clique vira um ESC "solto"
                    // por engano - e isso fechava o programa inteiro sozinho. Agora ESC só fecha
                    // a janela ativa (se houver alguma aberta); sair de verdade é só por [Sair].
                    var activeToClose = _openWindows.LastOrDefault(w => !w.IsMinimized);
                    if (activeToClose is not null)
                    {
                        CloseWindow(activeToClose);
                        return true;
                    }
                    return true;
                }

                var active = _openWindows.LastOrDefault(w => !w.IsMinimized);
                if (active is not null && active.HandleKey(key))
                {
                    return true;
                }

                var ch = char.ToUpperInvariant(key.KeyChar);
                var button = _buttons.FirstOrDefault(b => b.Shortcut == ch);
                if (button is not null)
                {
                    LaunchOrToggle(button.Name);
                    return true;
                }

                return false;
            }

            if (ev.Mouse is not { } click)
            {
                return false;
            }

            if (click.Row == 1)
            {
                var action = HitTopBar(click.Column);
                if (!click.IsPress && action != TopBarAction.None)
                {
                    if (action == TopBarAction.Exit)
                    {
                        _running = false;
                        _openWindows.Clear();
                    }
                    else if (action == TopBarAction.Reopen)
                    {
                        LaunchOrToggle("APP_MENU");
                    }
                    return true;
                }
            }


            if (click.Row >= Console.WindowHeight - 2)
            {
                var iconIndex = GetTaskBarIconIndex(click.Column);
                var minimized = MinimizedWindows();
                if (iconIndex >= 0 && iconIndex < minimized.Count && !click.IsPress)
                {
                    RestoreWindow(minimized[iconIndex]);
                    return true;
                }
                return false;
            }

            var clickedWindow = _openWindows.LastOrDefault(w => w.ContainsPoint(click.Column, click.Row) && !w.IsMinimized);
            if (clickedWindow is not null)
            {
                BringToFront(clickedWindow);

                if (clickedWindow.TryHandleCaptionClick(click.Column, click.Row, out var action))
                {
                    if (!click.IsPress)
                    {
                        if (action == WindowAction.Close)
                        {
                            CloseWindow(clickedWindow);
                        }
                        else if (action == WindowAction.Minimize)
                        {
                            MinimizeWindow(clickedWindow);
                        }

                        return true;
                    }
                    return false;
                }

                if ((click.IsPress || click.IsDrag) && clickedWindow.IsOnTitleBar(click.Column, click.Row))
                {
                    if (_draggingWindow != clickedWindow)
                    {
                        _draggingWindow = clickedWindow;
                        _dragOffsetX = click.Column - clickedWindow.X;
                        _dragOffsetY = click.Row - clickedWindow.Y;
                        _draggingWindow.IsDragging = true;
                        _draggingWindow.GhostX = clickedWindow.X;
                        _draggingWindow.GhostY = clickedWindow.Y;
                    }
                    else
                    {
                        var targetX = Math.Max(0, Math.Min(click.Column - _dragOffsetX, Console.WindowWidth - _draggingWindow.Width));
                        var targetY = Math.Max(3, Math.Min(click.Row - _dragOffsetY, Console.WindowHeight - _draggingWindow.Height - 2));
                        if (targetX != _draggingWindow.X || targetY != _draggingWindow.Y)
                        {
                            _draggingWindow.GhostX = _draggingWindow.X;
                            _draggingWindow.GhostY = _draggingWindow.Y;
                            _draggingWindow.X = targetX;
                            _draggingWindow.Y = targetY;
                            return true;
                        }
                    }
                    return false;
                }

                if ((!click.IsPress && !click.IsDrag) && _draggingWindow == clickedWindow)
                {
                    _draggingWindow.IsDragging = false;
                    _draggingWindow.X = Math.Max(0, Math.Min(click.Column - _dragOffsetX, Console.WindowWidth - clickedWindow.Width));
                    _draggingWindow.Y = Math.Max(3, Math.Min(click.Row - _dragOffsetY, Console.WindowHeight - clickedWindow.Height - 2));
                    _draggingWindow = null;
                    return true;
                }

                if (!click.IsPress && !click.IsDrag)
                {
                    clickedWindow.HandleMousePress(click.Column, click.Row);
                    return true;
                }

                return false;
            }

            if (_draggingWindow is not null)
            {
                var targetX = Math.Max(0, Math.Min(click.Column - _dragOffsetX, Console.WindowWidth - _draggingWindow.Width));
                var targetY = Math.Max(3, Math.Min(click.Row - _dragOffsetY, Console.WindowHeight - _draggingWindow.Height - 2));

                if (!click.IsPress && !click.IsDrag)
                {
                    _draggingWindow.IsDragging = false;
                    _draggingWindow.X = targetX;
                    _draggingWindow.Y = targetY;
                    _draggingWindow = null;
                    return true;
                }

                if (targetX != _draggingWindow.X || targetY != _draggingWindow.Y)
                {
                    _draggingWindow.GhostX = _draggingWindow.X;
                    _draggingWindow.GhostY = _draggingWindow.Y;
                    _draggingWindow.X = targetX;
                    _draggingWindow.Y = targetY;
                    return true;
                }
            }

            return false;
        }

        private int GetTaskBarIconIndex(int column)
        {
            var minimized = MinimizedWindows();
            var col = 0;
            for (var i = 0; i < minimized.Count; i++)
            {
                if (col + IconWidth > Console.WindowWidth)
                {
                    break;
                }

                if (column >= col && column < col + IconWidth)
                {
                    return i;
                }

                col += IconWidth + 1;
            }
            return -1;
        }

        private TopBarAction HitTopBar(int column)
        {
            if (column >= _topReopenStart && column < _topReopenEnd)
            {
                return TopBarAction.Reopen;
            }

            if (column >= _topExitStart && column < _topExitEnd)
            {
                return TopBarAction.Exit;
            }

            return TopBarAction.None;
        }

        private void BringToFront(AppWindow window)
        {
            if (_openWindows.Remove(window))
            {
                _openWindows.Add(window);
            }
        }

        private void RestoreWindow(AppWindow window)
        {
            window.IsMinimized = false;
            BringToFront(window);
        }

        private void MinimizeWindow(AppWindow window)
        {
            window.IsMinimized = true;
        }

        private void CloseWindow(AppWindow window)
        {
            _openWindows.Remove(window);
            _lastClosedAppName = window.Name;
        }

        private void LaunchOrToggle(string appName)
        {
            if (appName.Equals("EXIT", StringComparison.OrdinalIgnoreCase))
            {
                _running = false;
                _openWindows.Clear();
                return;
            }

            var existing = _openWindows.FirstOrDefault(w => w.Name.Equals(appName, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                existing.IsMinimized = false;
                BringToFront(existing);
                return;
            }

            AppWindow window = appName.ToUpperInvariant() switch
            {
                "APP_MENU" => new AppMenuWindow(4, 4, Math.Min(48, Console.WindowWidth - 8), 18, _buttons, LaunchOrToggle),
                "XWIN_TEXT" or "XWINTEXT" => new XwinTextWindow(8, 6, 48, 14),
                "MJB" => new MjbWindow(14, 5, 46, 16),
                "CONTROL" => new ControlPanelWindow(6, 4, 58, 18, _currentForeColor, _currentBackColor, ApplyColorScheme),
                "CALC" => new CalculatorWindow(12, 7, 36, 14),
                "ABOUT" => new AboutWindow(16, 8, 42, 12),
                _ => new PlaceholderWindow(appName, 18, 9, 40, 10),
            };

            _openWindows.Add(window);
            BringToFront(window);
        }
    }

    private sealed record AppButton(string Name, string Label, char Shortcut);
    private sealed record ConfigEntry(string Name, string Description, char Shortcut);

    private static class XwinHelpers
    {
        public static readonly string[] Names =
        [
            "Preto", "Azul escuro", "Verde", "Ciano", "Vermelho", "Magenta", "Marrom", "Branco",
            "Cinza", "Azul claro", "Verde claro", "Ciano claro", "Vermelho claro", "Magenta claro",
            "Amarelo", "Branco alta intensidade"
        ];

        public static ConsoleColor ToConsoleColor(int dosColor)
        {
            return dosColor switch
            {
                0 => ConsoleColor.Black,
                1 => ConsoleColor.DarkBlue,
                2 => ConsoleColor.DarkGreen,
                3 => ConsoleColor.DarkCyan,
                4 => ConsoleColor.DarkRed,
                5 => ConsoleColor.DarkMagenta,
                6 => ConsoleColor.DarkYellow,
                7 => ConsoleColor.Gray,
                8 => ConsoleColor.DarkGray,
                9 => ConsoleColor.Blue,
                10 => ConsoleColor.Green,
                11 => ConsoleColor.Cyan,
                12 => ConsoleColor.Red,
                13 => ConsoleColor.Magenta,
                14 => ConsoleColor.Yellow,
                15 => ConsoleColor.White,
                _ => ConsoleColor.Gray,
            };
        }

        public static ConsoleColor ChooseContrast(ConsoleColor background)
        {
            return background switch
            {
                ConsoleColor.Black or ConsoleColor.DarkBlue or ConsoleColor.DarkGreen or ConsoleColor.DarkCyan or ConsoleColor.DarkRed or ConsoleColor.DarkMagenta or ConsoleColor.DarkYellow or ConsoleColor.Gray => ConsoleColor.White,
                _ => ConsoleColor.Black,
            };
        }

        public static void SaveColorSettings(int foreIndex, int backIndex)
        {
            var path = Path.Combine(OsbHomeDir, "OSB.CFG");
            var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : [];
            WriteSection(lines, "[FORECOLOR]", foreIndex.ToString());
            WriteSection(lines, "[BACKCOLOR]", backIndex.ToString());
            File.WriteAllLines(path, lines);
        }

        private static void WriteSection(List<string> lines, string header, string value)
        {
            var index = lines.FindIndex(line => line.Trim().Equals(header, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                lines.Add(header);
                lines.Add(value);
                return;
            }

            var target = index + 1;
            while (target < lines.Count && string.IsNullOrWhiteSpace(lines[target]))
                target++;

            if (target < lines.Count && !lines[target].Trim().StartsWith(";"))
            {
                lines[target] = value;
                return;
            }

            lines.Insert(index + 1, value);
        }
    }

    private sealed class AppMenuWindow : AppWindow
    {
        private readonly IReadOnlyList<AppButton> _buttons;
        private readonly Action<string> _onSelect;

        public AppMenuWindow(int x, int y, int width, int height, IReadOnlyList<AppButton> buttons, Action<string> onSelect)
            : base("APP_MENU", "Menu de Aplicativos", x, y, width, height)
        {
            _buttons = buttons;
            _onSelect = onSelect;
        }

        public override bool HandleKey(ConsoleKeyInfo key)
        {
            var ch = char.ToUpperInvariant(key.KeyChar);
            if (key.Key == ConsoleKey.Escape)
            {
                return false;
            }

            var button = _buttons.FirstOrDefault(b => b.Shortcut == ch);
            if (button is not null)
            {
                _onSelect(button.Name);
                return true;
            }

            return false;
        }

        public override void HandleMousePress(int col, int row)
        {
            var bodyTop = Y + 2;
            for (var index = 0; index < _buttons.Count; index++)
            {
                if (row != bodyTop + index)
                {
                    continue;
                }

                var left = X + 2;
                var button = _buttons[index];
                var label = $"[{button.Shortcut}] {button.Label}";
                var width = Math.Min(label.Length, Width - 4);
                if (col >= left && col < left + width)
                {
                    _onSelect(button.Name);
                    return;
                }
            }
        }

        public override void RenderBody()
        {
            var lines = new List<string> { "Escolha um aplicativo:", string.Empty };
            lines.AddRange(_buttons.Select(b => $"[{b.Shortcut}] {b.Label}"));
            lines.Add(string.Empty);
            lines.Add("Clique em um item ou pressione a tecla de atalho.");
            WriteLines(X + 2, Y + 2, Width - 4, lines);
        }
    }

    private enum TopBarAction { None, Reopen, Exit }
    private enum WindowAction { None, Close, Minimize }

    private abstract class AppWindow
    {
        public string Name { get; }
        public string Title { get; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; }
        public int Height { get; }
        public bool IsMinimized { get; set; }
        public bool IsDragging { get; set; }
        public int GhostX { get; set; }
        public int GhostY { get; set; }

        protected AppWindow(string name, string title, int x, int y, int width, int height)
        {
            Name = name;
            Title = title;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public virtual void Tick() { }
        public abstract void RenderBody();
        public virtual bool HandleKey(ConsoleKeyInfo key) => false;
        public virtual void HandleMousePress(int col, int row) { }

        public static ConsoleColor WindowForeground { get; set; } = ConsoleColor.Gray;
        public static ConsoleColor WindowBackground { get; set; } = ConsoleColor.Black;
        public static ConsoleColor WindowTitleForeground { get; set; } = ConsoleColor.Black;
        public static ConsoleColor WindowTitleBackground { get; set; } = ConsoleColor.White;

        public void Render()
        {
            var left = Math.Max(0, Math.Min(X, Console.WindowWidth - Width));
            var top = Math.Max(3, Math.Min(Y, Console.WindowHeight - Height - 2));
            var oldFg = Console.ForegroundColor;
            var oldBg = Console.BackgroundColor;

            if (IsDragging)
            {
                // Sombra flutuante 3D ao arrastar a janela
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.BackgroundColor = ConsoleColor.Black;
                for (var r = 1; r <= Height; r++)
                {
                    var sRow = top + r;
                    var sCol = left + Width;
                    if (sRow < Console.WindowHeight - 2 && sCol < Console.WindowWidth)
                    {
                        Console.SetCursorPosition(sCol, sRow);
                        Console.Write("░");
                    }
                }
                var bRow = top + Height;
                if (bRow < Console.WindowHeight - 2)
                {
                    for (var c = 1; c <= Width; c++)
                    {
                        var sCol = left + c;
                        if (sCol < Console.WindowWidth)
                        {
                            Console.SetCursorPosition(sCol, bRow);
                            Console.Write("░");
                        }
                    }
                }
            }

            Console.ForegroundColor = IsDragging ? ConsoleColor.Yellow : WindowTitleForeground;
            Console.BackgroundColor = IsDragging ? ConsoleColor.DarkBlue : WindowTitleBackground;
            var displayTitle = IsDragging ? $"≡ [Arrastando] {Title} ≡" : Title;
            DrawBox(left, top, Width, Height, displayTitle);

            Console.ForegroundColor = WindowForeground;
            Console.BackgroundColor = WindowBackground;
            for (var row = 1; row < Height - 1; row++)
            {
                Console.SetCursorPosition(left + 1, top + row);
                Console.Write(new string(' ', Width - 2));
            }
            Console.SetCursorPosition(left + 1, top + 1);
            RenderBody();

            Console.ForegroundColor = oldFg;
            Console.BackgroundColor = oldBg;
        }

        public bool ContainsPoint(int col, int row)
            => col >= X && col < X + Width && row >= Y && row < Y + Height;

        public bool IsOnTitleBar(int col, int row)
            => row == Y && col >= X && col < X + Width;

        public bool TryHandleCaptionClick(int col, int row, out WindowAction action)
        {
            action = WindowAction.None;
            if (row != Y)
            {
                return false;
            }

            var minimizeX = X + Width - 8;
            var closeX = X + Width - 5;
            if (col >= closeX && col < closeX + 3)
            {
                action = WindowAction.Close;
                return true;
            }
            if (col >= minimizeX && col < minimizeX + 3)
            {
                action = WindowAction.Minimize;
                return true;
            }
            return false;
        }

        protected static void DrawBox(int x, int y, int width, int height, string title)
        {
            var right = x + width - 1;
            var bottom = y + height - 1;
            Console.SetCursorPosition(x, y);
            Console.Write('╔' + new string('═', width - 2) + '╗');
            for (var row = y + 1; row < bottom; row++)
            {
                Console.SetCursorPosition(x, row);
                Console.Write('║' + new string(' ', width - 2) + '║');
            }
            Console.SetCursorPosition(x, bottom);
            Console.Write('╚' + new string('═', width - 2) + '╝');
            var titleText = $" {title} ";
            if (titleText.Length > width - 10)
            {
                titleText = titleText[..(width - 10)];
            }

            Console.SetCursorPosition(x + 1, y);
            Console.Write(titleText.PadRight(width - 10));
            Console.SetCursorPosition(x + width - 8, y);
            Console.Write("[_][X]");
        }

        protected static void WriteLines(int x, int y, int width, IEnumerable<string> lines)
        {
            var row = y;
            foreach (var line in lines)
            {
                Console.SetCursorPosition(x, row++);
                var text = line.Length <= width ? line : line[..width];
                Console.Write(text.PadRight(width));
            }
        }
    }

    private sealed class PlaceholderWindow : AppWindow
    {
        private readonly string _message;

        public PlaceholderWindow(string name, int x, int y, int width, int height)
            : base(name, name, x, y, width, height)
        {
            _message = "Aplicativo ainda não portado para .NET.";
        }

        public override void RenderBody()
        {
            WriteLines(X + 2, Y + 2, Width - 4, [
                $"Aplicativo: {Name}",
                string.Empty,
                _message,
                string.Empty,
                "Feche ou minimize esta janela."
            ]);
        }
    }

    private sealed class AboutWindow : AppWindow
    {
        public AboutWindow(int x, int y, int width, int height)
            : base("ABOUT", "Sobre XWIN", x, y, width, height)
        {
        }

        public override void RenderBody()
        {
            var lines = new[]
            {
                "XWIN .NET 10 - modo texto",
                string.Empty,
                "Interfaces em janelas ASCII",
                "com suporte a minimizar e fechar.",
                string.Empty,
                "Clique no título para arrastar.",
                "Clique nos botões [_] e [X].",
                string.Empty,
                "Esc volta para o OSB."
            };
            WriteLines(X + 2, Y + 2, Width - 4, lines);
        }
    }

    private sealed class CalculatorWindow : AppWindow
    {
        private string _formula = string.Empty;
        private string _result = string.Empty;
        private int _cursor = 0;

        public CalculatorWindow(int x, int y, int width, int height)
            : base("CALC", "Calculadora", x, y, width, height)
        {
        }

        private readonly string[][] _buttonRows =
        [
            ["7", "8", "9", "/"],
            ["4", "5", "6", "*"],
            ["1", "2", "3", "-"],
            ["0", ".", "^", "+"],
            ["C", "sqrt", "="]
        ];

        public override bool HandleKey(ConsoleKeyInfo key)
        {
            if (key.Key == ConsoleKey.Backspace && _cursor > 0)
            {
                _formula = _formula.Remove(_cursor - 1, 1);
                _cursor--;
                return true;
            }
            if (key.Key == ConsoleKey.LeftArrow && _cursor > 0)
            {
                _cursor--;
                return true;
            }
            if (key.Key == ConsoleKey.RightArrow && _cursor < _formula.Length)
            {
                _cursor++;
                return true;
            }
            if (key.Key == ConsoleKey.Enter)
            {
                _result = Evaluate(_formula);
                return true;
            }
            if (key.Key == ConsoleKey.Escape)
            {
                _formula = string.Empty;
                _result = string.Empty;
                _cursor = 0;
                return true;
            }
            if (!char.IsControl(key.KeyChar) && "0123456789+-*/^.()sS".Contains(key.KeyChar))
            {
                var text = key.KeyChar.ToString();
                if (char.ToUpperInvariant(key.KeyChar) == 'S')
                {
                    text = "sqrt(";
                }
                _formula = _formula.Insert(_cursor, text);
                _cursor += text.Length;
                return true;
            }

            return false;
        }

        public override void HandleMousePress(int col, int row)
        {
            var buttonTop = Y + 12;
            for (var rowIndex = 0; rowIndex < _buttonRows.Length; rowIndex++)
            {
                var buttonRow = _buttonRows[rowIndex];
                var top = buttonTop + rowIndex;
                if (row != top)
                {
                    continue;
                }

                var left = X + 2;
                if (rowIndex < 4)
                {
                    for (var colIndex = 0; colIndex < buttonRow.Length; colIndex++)
                    {
                        var buttonText = buttonRow[colIndex];
                        if (col >= left && col < left + 7)
                        {
                            ExecuteButton(buttonText);
                            return;
                        }
                        left += 8;
                    }
                }
                else
                {
                    var widths = new[] { 10, 10, 10 };
                    for (var colIndex = 0; colIndex < buttonRow.Length; colIndex++)
                    {
                        var width = widths[colIndex];
                        if (col >= left && col < left + width)
                        {
                            ExecuteButton(buttonRow[colIndex]);
                            return;
                        }
                        left += width + 1;
                    }
                }
            }
        }

        private void ExecuteButton(string buttonText)
        {
            switch (buttonText)
            {
                case "C":
                    _formula = string.Empty;
                    _result = string.Empty;
                    _cursor = 0;
                    break;
                case "=":
                    _result = Evaluate(_formula);
                    break;
                case "sqrt":
                    _formula = _formula.Insert(_cursor, "sqrt(");
                    _cursor += 5;
                    break;
                default:
                    _formula = _formula.Insert(_cursor, buttonText);
                    _cursor += buttonText.Length;
                    break;
            }
        }

        public override void RenderBody()
        {
            var lines = new List<string>
            {
                "Calculadora ASCII",
                string.Empty,
                "Use números e + - * / ^ . ( ) e sqrt()",
                "Enter ou clique em = para calcular.",
                string.Empty,
                "Expressão:",
                _formula,
                string.Empty,
                "Resultado:",
                _result,
                string.Empty,
                "Clique nos botões abaixo ou use o teclado. Esc limpa.",
                string.Empty
            };
            WriteLines(X + 2, Y + 2, Width - 4, lines);
            var buttonTop = Y + 14;
            var left = X + 2;
            for (var rowIndex = 0; rowIndex < _buttonRows.Length; rowIndex++)
            {
                var buttonRow = _buttonRows[rowIndex];
                if (rowIndex < 4)
                {
                    for (var colIndex = 0; colIndex < buttonRow.Length; colIndex++)
                    {
                        var label = $"[{buttonRow[colIndex]}]".PadRight(6);
                        Console.SetCursorPosition(left, buttonTop + rowIndex);
                        Console.Write(label);
                        left += 8;
                    }
                }
                else
                {
                    var widths = new[] { 10, 10, 10 };
                    for (var colIndex = 0; colIndex < buttonRow.Length; colIndex++)
                    {
                        var label = $"[{buttonRow[colIndex]}]".PadRight(widths[colIndex]);
                        Console.SetCursorPosition(left, buttonTop + rowIndex);
                        Console.Write(label);
                        left += widths[colIndex] + 1;
                    }
                }
                left = X + 2;
            }

            var cursorRow = Y + 2 + 6;
            var cursorCol = X + 2 + Math.Min(_cursor, Width - 5);
            if (cursorRow < Console.WindowHeight && cursorCol < Console.WindowWidth)
            {
                Console.SetCursorPosition(cursorCol, cursorRow);
                Console.CursorVisible = true;
            }
        }

        private static string Evaluate(string expression)
        {
            try
            {
                var cleaned = new string(expression.Where(c => "0123456789+-*/^.()sqrt ".Contains(c)).ToArray());
                var value = SimpleCalculator.Evaluate(cleaned);
                return value.ToString();
            }
            catch (Exception ex)
            {
                return "Erro: " + ex.Message;
            }
        }
    }

    private sealed class XwinTextWindow : AppWindow
    {
        private readonly List<string> _lines = [string.Empty];
        private int _row;
        private int _col;

        public XwinTextWindow(int x, int y, int width, int height)
            : base("XWIN_TEXT", "XWinText", x, y, width, height)
        {
        }

        public override bool HandleKey(ConsoleKeyInfo key)
        {
            if (key.Key == ConsoleKey.Backspace)
            {
                if (_col > 0)
                {
                    _lines[_row] = _lines[_row].Remove(_col - 1, 1);
                    _col--;
                }
                else if (_row > 0)
                {
                    var previous = _lines[_row - 1];
                    _col = previous.Length;
                    _lines[_row - 1] += _lines[_row];
                    _lines.RemoveAt(_row);
                    _row--;
                }

                return true;
            }
            if (key.Key == ConsoleKey.Enter)
            {
                var current = _lines[_row];
                var remainder = current[_col..];
                _lines[_row] = current[.._col];
                _lines.Insert(_row + 1, remainder);
                _row++;
                _col = 0;
                return true;
            }
            if (key.Key == ConsoleKey.LeftArrow && _col > 0)
            {
                _col--;
                return true;
            }
            if (key.Key == ConsoleKey.RightArrow && _col < _lines[_row].Length)
            {
                _col++;
                return true;
            }
            if (key.Key == ConsoleKey.UpArrow && _row > 0)
            {
                _row--;
                _col = Math.Min(_col, _lines[_row].Length);
                return true;
            }
            if (key.Key == ConsoleKey.DownArrow && _row < _lines.Count - 1)
            {
                _row++;
                _col = Math.Min(_col, _lines[_row].Length);
                return true;
            }
            if (!char.IsControl(key.KeyChar))
            {
                _lines[_row] = _lines[_row].Insert(_col, key.KeyChar.ToString());
                _col++;
                return true;
            }

            return false;
        }

        public override void RenderBody()
        {
            var lines = new List<string>
            {
                "Editor XWinText",
                string.Empty,
                "Digite texto diretamente nesta janela.",
                "Setas movimentam, Backspace apaga."
            };
            var bodyTop = Y + 2;
            WriteLines(X + 2, bodyTop, Width - 4, lines);

            var editTop = bodyTop + lines.Count + 1;
            for (var i = 0; i < Height - (editTop - Y) - 2; i++)
            {
                var text = i < _lines.Count ? _lines[i] : string.Empty;
                Console.SetCursorPosition(X + 2, editTop + i);
                Console.Write(text.PadRight(Width - 4));
            }

            var cursorRow = editTop + _row;
            var cursorCol = X + 2 + Math.Min(_col, Width - 5);
            if (cursorRow < Console.WindowHeight && cursorCol < Console.WindowWidth)
            {
                Console.SetCursorPosition(cursorCol, cursorRow);
                Console.CursorVisible = true;
            }
        }
    }

    private sealed class ControlPanelWindow : AppWindow
    {
        private int _selectedFore;
        private int _selectedBack;
        private readonly Action<int, int> _applyColors;

        public ControlPanelWindow(int x, int y, int width, int height, int currentFore, int currentBack, Action<int, int> applyColors)
            : base("CONTROL", "Painel de Controle", x, y, width, height)
        {
            _selectedFore = currentFore;
            _selectedBack = currentBack;
            _applyColors = applyColors;
        }

        public override bool HandleKey(ConsoleKeyInfo key)
        {
            if (key.Key == ConsoleKey.Escape)
            {
                return false;
            }

            if (key.Key == ConsoleKey.Enter)
            {
                _applyColors(_selectedFore, _selectedBack);
                XwinHelpers.SaveColorSettings(_selectedFore, _selectedBack);
                return true;
            }

            if (key.Key == ConsoleKey.Tab)
            {
                (_selectedFore, _selectedBack) = (_selectedBack, _selectedFore);
                return true;
            }

            return false;
        }

        public override void HandleMousePress(int col, int row)
        {
            var foregroundTop = Y + 6;
            var backgroundTop = Y + 10;
            var swatchWidth = 6;
            for (var index = 0; index < 8; index++)
            {
                var left = X + 2 + index * (swatchWidth + 1);
                if (row == foregroundTop && col >= left && col < left + swatchWidth)
                {
                    SelectForeground(index);
                }

                if (row == foregroundTop + 1 && col >= left && col < left + swatchWidth)
                {
                    SelectForeground(index + 8);
                }

                if (row == backgroundTop && col >= left && col < left + swatchWidth)
                {
                    SelectBackground(index);
                }

                if (row == backgroundTop + 1 && col >= left && col < left + swatchWidth)
                {
                    SelectBackground(index + 8);
                }
            }

            var saveLeft = X + 2;
            var saveTop = Y + Height - 3;
            if (row == saveTop && col >= saveLeft && col < saveLeft + 12)
            {
                _applyColors(_selectedFore, _selectedBack);
                XwinHelpers.SaveColorSettings(_selectedFore, _selectedBack);
            }
        }

        private void SelectForeground(int index)
        {
            _selectedFore = index;
            _applyColors(_selectedFore, _selectedBack);
        }

        private void SelectBackground(int index)
        {
            _selectedBack = index;
            _applyColors(_selectedFore, _selectedBack);
        }

        public override void RenderBody()
        {
            var lines = new List<string>
            {
                "Configuração de Cores",
                string.Empty,
                "Escolha a cor da letra e do fundo para o XWIN.",
                string.Empty,
                $"Cor atual da letra: {_selectedFore} - {XwinHelpers.Names[_selectedFore]}",
                $"Cor atual do fundo: {_selectedBack} - {XwinHelpers.Names[_selectedBack]}",
                string.Empty,
                "Clique em uma cor ou pressione Enter para salvar.",
                string.Empty
            };
            WriteLines(X + 2, Y + 2, Width - 4, lines);

            RenderColorRow(Y + 6, _selectedFore, true);
            RenderColorRow(Y + 10, _selectedBack, false);

            Console.SetCursorPosition(X + 2, Y + Height - 3);
            Console.Write("[Enter] Aplicar agora   [Tab] Trocar foco   [Esc] Fechar");
        }

        private void RenderColorRow(int top, int selectedIndex, bool isForeground)
        {
            var swatchWidth = 6;
            var left = X + 2;
            for (var index = 0; index < 8; index++)
            {
                RenderColorSwatch(left, top, index, selectedIndex == index, isForeground);
                left += swatchWidth + 1;
            }
            left = X + 2;
            for (var index = 8; index < 16; index++)
            {
                RenderColorSwatch(left, top + 1, index, selectedIndex == index, isForeground);
                left += swatchWidth + 1;
            }
        }

        private void RenderColorSwatch(int x, int y, int index, bool selected, bool isForeground)
        {
            var oldFg = Console.ForegroundColor;
            var oldBg = Console.BackgroundColor;
            var color = XwinHelpers.ToConsoleColor(index);
            Console.ForegroundColor = isForeground ? color : XwinHelpers.ChooseContrast(color);
            Console.BackgroundColor = isForeground ? Console.BackgroundColor : color;
            var label = index.ToString().PadLeft(2, ' ');
            Console.SetCursorPosition(x, y);
            Console.Write(selected ? $"[{label}]" : $" {label} ");
            Console.ForegroundColor = oldFg;
            Console.BackgroundColor = oldBg;
        }
    }

    private sealed class MjbWindow : AppWindow
    {
        private int _frame;

        public MjbWindow(int x, int y, int width, int height)
            : base("MJB", "MJB", x, y, width, height)
        {
        }

        public override void Tick()
        {
            _frame = (_frame + 1) % 8;
        }

        public override void RenderBody()
        {
            var art = new[]
            {
                "MJB - Multitarefa em texto",
                string.Empty,
                "╔════════╗  ╔════════╗",
                "║  ♫ ♫  ║  ║  ■ ■  ║",
                "║  ♪ ♪  ║  ║  ■ ■  ║",
                "╚════════╝  ╚════════╝",
                string.Empty,
                "Clique em [_] para minimizar,",
                "em [X] para fechar.",
                string.Empty,
                $"Frame: {_frame + 1} / 8"
            };
            WriteLines(X + 2, Y + 2, Width - 4, art);
        }
    }

    private static class SimpleCalculator
    {
        public static decimal Evaluate(string expression)
        {
            var tokens = Tokenize(expression).ToList();
            var values = new Stack<decimal>();
            var ops = new Stack<char>();
            foreach (var token in tokens)
            {
                if (decimal.TryParse(token, out var number))
                {
                    values.Push(number);
                    continue;
                }

                if (token == "(")
                {
                    ops.Push('(');
                    continue;
                }
                if (token == ")")
                {
                    while (ops.Count > 0 && ops.Peek() != '(')
                        ApplyTop(values, ops);
                    if (ops.Count > 0)
                    {
                        ops.Pop();
                    }

                    continue;
                }

                var op = token[0];
                while (ops.Count > 0 && Precedence(ops.Peek()) >= Precedence(op))
                    ApplyTop(values, ops);
                ops.Push(op);
            }

            while (ops.Count > 0)
                ApplyTop(values, ops);

            return values.Count > 0 ? values.Pop() : 0m;
        }

        private static IEnumerable<string> Tokenize(string expression)
        {
            var builder = new StringBuilder();
            foreach (var ch in expression)
            {
                if (char.IsWhiteSpace(ch))
                {
                    continue;
                }

                if (char.IsDigit(ch) || ch == '.')
                {
                    builder.Append(ch);
                    continue;
                }

                if (builder.Length > 0)
                {
                    yield return builder.ToString();
                    builder.Clear();
                }

                yield return ch.ToString();
            }

            if (builder.Length > 0)
            {
                yield return builder.ToString();
            }
        }

        private static void ApplyTop(Stack<decimal> values, Stack<char> ops)
        {
            if (ops.Count == 0)
            {
                return;
            }

            var op = ops.Pop();
            if (op == 's')
            {
                if (values.Count < 1)
                {
                    throw new InvalidOperationException("Operador sqrt sem operandos");
                }

                var operand = values.Pop();
                values.Push((decimal)Math.Sqrt((double)operand));
                return;
            }

            if (values.Count < 2)
            {
                return;
            }

            var right = values.Pop();
            var left = values.Pop();
            values.Push(op switch
            {
                '+' => left + right,
                '-' => left - right,
                '*' => left * right,
                '/' => right == 0 ? 0 : left / right,
                '^' => (decimal)Math.Pow((double)left, (double)right),
                _ => throw new InvalidOperationException($"Operador inválido: {op}")
            });
        }

        private static int Precedence(char op) => op switch
        {
            '+' => 1,
            '-' => 1,
            '*' => 2,
            '/' => 2,
            _ => 0
        };
    }
}
