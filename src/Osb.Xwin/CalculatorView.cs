using System;
using System.Collections.Generic;
using System.Linq;
using Terminal.Gui;

namespace Osb.Xwin;

internal class CalculatorView : AppView
{
    private readonly TextField _display;

    public CalculatorView() : base("Calculadora XWin")
    {
        X = 0;
        Y = 0;
        Width = 42;
        Height = 15;

        _display = new TextField("0")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
            CanFocus = false,
            TabStop = false,
        };

        var closeButton = new Button("[X]")
        {
            X = 36,
            Y = 0,
            Width = 4,
            Height = 1,
        };
        closeButton.Clicked += () => CloseWindow();

        var minimizeButton = new Button("[_]")
        {
            X = 32,
            Y = 0,
            Width = 4,
            Height = 1,
        };
        minimizeButton.Clicked += () => MinimizeWindow();

        var buttons = new[]
        {
            new[] { "C", "(", ")", "/", "sqrt" },
            new[] { "7", "8", "9", "*", "^" },
            new[] { "4", "5", "6", "-", "%" },
            new[] { "1", "2", "3", "+", "=" },
            new[] { "0", ".", "BKSP" },
        };

        var y = 2;
        foreach (var row in buttons)
        {
            var x = 0;
            foreach (var btn in row)
            {
                var width = btn.Length == 1 ? 6 : 10;
                var button = new Button(btn)
                {
                    X = x,
                    Y = y,
                    Width = width,
                    Height = 1,
                    TabStop = false,
                };
                button.Clicked += () =>
                {
                    OnCalculatorButton(btn);
                    _display.SetFocus();
                };
                Add(button);
                x += width + 1;
            }
            y += 2;
        }

        Add(_display);
        Add(closeButton);
        Add(minimizeButton);
    }

    public override bool OnKeyDown(KeyEvent key)
    {
        if (key.Key == Key.Enter)
        {
            OnCalculatorButton("=");
            return true;
        }
        if (key.Key == Key.Esc)
        {
            OnCalculatorButton("C");
            return true;
        }
        if (key.Key == Key.Backspace)
        {
            OnCalculatorButton("BKSP");
            return true;
        }

        var ch = key.Key.ToString();
        if (ch.Length == 1)
        {
            if (char.IsDigit(ch[0]))
            {
                OnCalculatorButton(ch);
                return true;
            }
            if ("+-*/^.()%".Contains(ch))
            {
                OnCalculatorButton(ch);
                return true;
            }
        }

        return base.OnKeyDown(key);
    }

    private void OnCalculatorButton(string btn)
    {
        if (btn == "C")
        {
            _display.Text = "0";
        }
        else if (btn == "=")
        {
            try
            {
                var text = _display.Text?.ToString() ?? "0";
                var result = SimpleCalculator.Evaluate(text);
                _display.Text = result.ToString("0.######");
            }
            catch
            {
                _display.Text = "Erro";
            }
        }
        else if (btn == "BKSP")
        {
            var text = _display.Text?.ToString() ?? "0";
            if (text.Length > 1)
            {
                _display.Text = text[..^1];
            }
            else
            {
                _display.Text = "0";
            }
        }
        else if (btn == "sqrt")
        {
            _display.Text = _display.Text?.ToString() ?? "0";
            _display.Text += "sqrt(";
        }
        else
        {
            var current = _display.Text?.ToString() ?? "0";
            if (current == "0" && btn != ".")
            {
                _display.Text = btn;
            }
            else
            {
                _display.Text = current + btn;
            }
        }
    }

    private void CloseWindow()
    {
        var parent = SuperView as XwinApp;
        parent?.CloseWindow(this);
    }

    private void MinimizeWindow()
    {
        var parent = SuperView as XwinApp;
        parent?.MinimizeWindow(this);
        Visible = false;
    }
}
