using Terminal.Gui;

namespace Osb.Xwin;

internal class PlaceholderView : AppView
{
    public PlaceholderView(string appName) : base(appName)
    {
        X = 0;
        Y = 0;
        Width = 42;
        Height = 6;

        var titleBar = new Label("[_][X]")
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
        };

        var label = new Label($"Aplicativo: {appName}\n\nAplicativo ainda nao portado para .NET.\n\nFeche ou minimize esta janela.")
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
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
