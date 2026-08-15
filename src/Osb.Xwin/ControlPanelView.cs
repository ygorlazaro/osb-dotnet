using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Terminal.Gui;

namespace Osb.Xwin;

internal class ControlPanelView : AppView
{
    public ControlPanelView() : base("Painel de Controle")
    {
        X = 0;
        Y = 0;
        Width = 62;
        Height = 6;

        var titleBar = new Label("[_][X]")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
        };

        var label = new Label("Painel de Controle - em desenvolvimento.")
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };

        var closeButton = new Button("[X]")
        {
            X = 56,
            Y = 0,
            Width = 4,
            Height = 1,
        };
        closeButton.Clicked += () => CloseWindow();

        var minimizeButton = new Button("[_]")
        {
            X = 52,
            Y = 0,
            Width = 4,
            Height = 1,
        };
        minimizeButton.Clicked += () => MinimizeWindow();

        Add(titleBar);
        Add(label);
        Add(closeButton);
        Add(minimizeButton);
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
