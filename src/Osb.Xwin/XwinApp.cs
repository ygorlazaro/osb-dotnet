using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terminal.Gui;

namespace Osb.Xwin;

internal class XwinApp : Toplevel
{
    private static readonly string OsbHomeDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".osb");

    private readonly List<AppView> _openWindows = [];
    private readonly List<AppView> _taskbarItems = [];
    private ListView _taskbar;
    private int _currentForeColor = 15;
    private int _currentBackColor = 1;

    public XwinApp() : base()
    {
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();

        var scheme = LoadColorScheme();
        ApplyColorScheme(scheme.ForeIndex, scheme.BackIndex);

        var menuBar = new MenuBar(new[] {
            new MenuBarItem("XWIN", new MenuItem[] {
                new("_Reabrir Menu", "ReabrirMenu", () => OpenAppMenu()),
                new("_Sair", "Sair", () => ExitXWin())
            })
        });
        Add(menuBar);

        _taskbar = new ListView(new string[0])
        {
            X = 0,
            Y = 22,
            Width = Dim.Fill(),
            Height = 1,
        };
        _taskbar.SelectedItemChanged += (e) =>
        {
            if (_taskbar.SelectedItem >= 0 && _taskbar.SelectedItem < _taskbarItems.Count)
            {
                var window = _taskbarItems[_taskbar.SelectedItem];
                RestoreWindow(window);
            }
        };
        Add(_taskbar);

        var statusBar = new StatusBar(new StatusItem[] {
            new StatusItem(Key.Enter, "~Enter~ Abrir Menu", () => OpenAppMenu()),
            new StatusItem(Key.Esc, "~Esc~ Fechar Janela", () => CloseActiveWindow()),
        });
        Add(statusBar);
    }

    private void ExitXWin()
    {
        _openWindows.Clear();
        Application.RequestStop();
    }

    private void OpenAppMenu()
    {
        CloseActiveWindow();
        var buttons = LoadAppButtons();
        AppMenuView? menuView = null;
        menuView = new AppMenuView(buttons, appName =>
        {
            if (menuView != null)
            {
                CloseWindow(menuView);
            }
            LaunchOrToggle(appName);
        });
        Add(menuView);
        _openWindows.Add(menuView);
        menuView.SetFocus();
    }

    private void LaunchOrToggle(string appName)
    {
        if (appName.Equals("EXIT", StringComparison.OrdinalIgnoreCase))
        {
            ExitXWin();
            return;
        }

        var existing = _openWindows.LastOrDefault(w => w.Title is not null);
        if (existing is not null)
        {
            var title = existing.Title.ToString()!;
            if (title.IndexOf(appName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (!existing.Visible)
                {
                    RestoreWindow(existing);
                }
                else
                {
                    existing.SetFocus();
                }
                return;
            }
        }

        AppView view = appName.ToUpperInvariant() switch
        {
            "APP_MENU" => CreateAppMenuWindow(),
            "XWIN_TEXT" or "XWINTEXT" or "EDIT" or "KISS" => CreateTextEditorWindow(),
            "CALC" or "CALCULATOR" => CreateCalculatorWindow(),
            "CALENDAR" or "CAL" => CreateCalendarWindow(),
            "CLOCK" or "RELOGIO" or "TIME" => CreateClockWindow(),
            "CONTROL" => CreateControlPanelWindow(),
            "ABOUT" => CreateAboutWindow(),
            _ => CreatePlaceholderWindow(appName),
        };

        _openWindows.Add(view);
        Add(view);
        view.SetFocus();
    }

    private AppView CreateAppMenuWindow()
    {
        var buttons = LoadAppButtons();
        AppMenuView? menuView = null;
        menuView = new AppMenuView(buttons, appName =>
        {
            if (menuView != null)
            {
                CloseWindow(menuView);
            }
            LaunchOrToggle(appName);
        });
        return menuView;
    }

    private AppView CreateTextEditorWindow()
    {
        return new TextEditorView(OsbHomeDir);
    }

    private AppView CreateCalculatorWindow()
    {
        return new CalculatorView();
    }

    private AppView CreateCalendarWindow()
    {
        return new CalendarView();
    }

    private AppView CreateClockWindow()
    {
        return new ClockView();
    }

    private AppView CreateControlPanelWindow()
    {
        return new ControlPanelView();
    }

    private AppView CreateAboutWindow()
    {
        return new AboutView();
    }

    private AppView CreatePlaceholderWindow(string appName)
    {
        return new PlaceholderView(appName);
    }

    public void MinimizeWindow(AppView window)
    {
        window.Visible = false;
        _taskbarItems.Add(window);
        UpdateTaskbar();
    }

    public void CloseWindow(AppView window)
    {
        _openWindows.Remove(window);
        _taskbarItems.Remove(window);
        Remove(window);
        window.Dispose();
        UpdateTaskbar();
    }

    private void CloseActiveWindow()
    {
        var active = _openWindows.LastOrDefault();
        if (active is not null)
        {
            CloseWindow(active);
        }
    }

    private (int ForeIndex, int BackIndex) LoadColorScheme()
    {
        var path = Path.Combine(OsbHomeDir, "OSB.CFG");
        if (!File.Exists(path))
        {
            return (15, 1);
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

        return (fore, back);
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

    private void ApplyColorScheme(int foreIndex, int backIndex)
    {
        _currentForeColor = foreIndex;
        _currentBackColor = backIndex;
    }

    private void UpdateTaskbar()
    {
        _taskbar.SetSource(_taskbarItems.Select(w => w.Title ?? string.Empty).ToArray());
    }

    private void RestoreWindow(AppView window)
    {
        window.Visible = true;
        window.SetFocus();
        _taskbarItems.Remove(window);
        UpdateTaskbar();
    }

    private IReadOnlyList<AppButton> LoadAppButtons()
    {
        var path = Path.Combine(OsbHomeDir, "CONF", "XWIN.CFG");
        if (!File.Exists(path))
        {
            return new List<AppButton>
            {
                new("CONTROL", "Painel de Controle", 'P'),
                new("CALC", "Calculadora XWin", 'C'),
                new("XWIN_TEXT", "Editor de Texto", 'E'),
                new("CALENDAR", "Calendário", 'L'),
                new("CLOCK", "Relógio Digital", 'R'),
                new("ABOUT", "Sobre o XWIN", 'A'),
            };
        }

        var lines = File.ReadAllLines(path);
        var entries = new List<AppButton>();
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
            entries.Add(new AppButton(name, description, shortcut));
        }

        entries.Add(new AppButton("CONTROL", "Painel de Controle", 'P'));
        entries.Add(new AppButton("CALC", "Calculadora XWin", 'C'));
        entries.Add(new AppButton("XWIN_TEXT", "Editor de Texto", 'E'));
        entries.Add(new AppButton("CALENDAR", "Calendário", 'L'));
        entries.Add(new AppButton("CLOCK", "Relógio Digital", 'R'));
        entries.Add(new AppButton("ABOUT", "Sobre o XWIN", 'A'));

        return entries;
    }
}
