using System;
using System.IO;
using System.Linq;
using Terminal.Gui;

namespace Osb.Xwin;

internal class ControlPanelView : AppView
{
    private readonly Label[,] _swatches = new Label[4, 4];
    private readonly string[] _colorNames =
    {
        "Preto", "Vermelho", "Verde", "Amarelo",
        "Azul", "Magenta", "Ciano", "Branco",
        "Cinza", "Vermelho Claro", "Verde Claro", "Amarelo Claro",
        "Azul Claro", "Magenta Claro", "Ciano Claro", "Branco Brilhante"
    };

    public ControlPanelView() : base("Painel de Controle")
    {
        X = 0;
        Y = 0;
        Width = 70;
        Height = 18;

        var titleBar = new Label("[_][X]")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
        };

        var iconLabel = new Label("Cor do Fundo")
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = 1,
        };

        var closeButton = new Button("[X]")
        {
            X = 64,
            Y = 0,
            Width = 4,
            Height = 1,
        };
        closeButton.Clicked += () => CloseWindow();

        var minimizeButton = new Button("[_]")
        {
            X = 60,
            Y = 0,
            Width = 4,
            Height = 1,
        };
        minimizeButton.Clicked += () => MinimizeWindow();

        var colorIndex = 0;
        for (var row = 0; row < 4; row++)
        {
            for (var col = 0; col < 4; col++)
            {
                var swatch = new Label("  ")
                {
                    X = col * 16,
                    Y = row * 3 + 4,
                    Width = 3,
                    Height = 1,
                };
                var nameLabel = new Label(_colorNames[colorIndex])
                {
                    X = col * 16,
                    Y = row * 3 + 5,
                    Width = 14,
                    Height = 1,
                };

                var colorValue = Color.Black;
                switch (colorIndex)
                {
                    case 0: colorValue = Color.Black; break;
                    case 1: colorValue = Color.Red; break;
                    case 2: colorValue = Color.Green; break;
                    case 3: colorValue = Color.Brown; break;
                    case 4: colorValue = Color.Blue; break;
                    case 5: colorValue = Color.Magenta; break;
                    case 6: colorValue = Color.Cyan; break;
                    case 7: colorValue = Color.White; break;
                    case 8: colorValue = Color.Gray; break;
                    case 9: colorValue = Color.BrightRed; break;
                    case 10: colorValue = Color.BrightGreen; break;
                    case 11: colorValue = Color.BrightYellow; break;
                    case 12: colorValue = Color.BrightBlue; break;
                    case 13: colorValue = Color.BrightMagenta; break;
                    case 14: colorValue = Color.BrightCyan; break;
                    case 15: colorValue = Color.White; break;
                }
                swatch.ColorScheme = new ColorScheme
                {
                    Normal = new Terminal.Gui.Attribute(Color.White, colorValue),
                    Focus = new Terminal.Gui.Attribute(Color.White, colorValue),
                    HotNormal = new Terminal.Gui.Attribute(Color.White, colorValue),
                    HotFocus = new Terminal.Gui.Attribute(Color.White, colorValue),
                    Disabled = new Terminal.Gui.Attribute(Color.White, colorValue),
                };

                var capturedIndex = colorIndex;
                var capturedColor = colorValue;
                swatch.Clicked += () => ApplyBackgroundColor(capturedColor, capturedIndex);
                nameLabel.Clicked += () => ApplyBackgroundColor(capturedColor, capturedIndex);

                Add(swatch);
                Add(nameLabel);
                _swatches[row, col] = swatch;
                colorIndex++;
            }
        }

        Add(titleBar);
        Add(iconLabel);
        Add(closeButton);
        Add(minimizeButton);
    }

    private void ApplyBackgroundColor(Color color, int colorIndex)
    {
        var parent = SuperView as XwinApp;
        parent?.SetBackgroundColor(colorIndex);
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
