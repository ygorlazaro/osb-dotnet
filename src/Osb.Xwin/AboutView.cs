using Terminal.Gui;

namespace Osb.Xwin;

internal class AboutView : AppView
{
    public AboutView() : base("Sobre XWIN")
    {
        X = 0;
        Y = 0;
        Width = 46;
        Height = 6;

        var titleBar = new Label("[_][X]")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
        };

        var label = new Label("XWIN .NET 10 - modo texto\n\nInterfaces em janelas ASCII\ncom suporte a minimizar e fechar.\n\nEsc volta para o OSB.")
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };

        var closeButton = new Button("[X]")
        {
            X = 40,
            Y = 0,
            Width = 4,
            Height = 1,
        };
        closeButton.Clicked += () => CloseWindow();

        var minimizeButton = new Button("[_]")
        {
            X = 36,
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
