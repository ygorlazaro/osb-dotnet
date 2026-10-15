// OSTK - application model. Message loop: pump input, repaint on change,
// draw the cursor. Desktop content is provided by Osb.Toolkit.Desktop.

using Osb.Xwin;

namespace Osb.Toolkit;

internal static unsafe class App
{
    private static bool _running;
    private static int _frameCount;

    public static void Run()
    {
        if (!Osb.Xwin.Gfx.Active) { Osb.Xwin.Gfx.Start(); Osb.Xwin.Gfx.Enter(); }
        _running = true;
        _frameCount = 0;

        while (_running)
        {
            _frameCount++;
            Osb.Xwin.Cursor.Restore();
            Osb.Xwin.Mouse.Refresh();
            Osb.Xwin.Keyboard.Refresh();

            int layoutBefore = Osb.Xwin.WindowManager.LayoutVersion;
            bool inputChanged = ProcessInput() ||
                                Osb.Xwin.WindowManager.LayoutVersion != layoutBefore;

            bool needPaint = _frameCount == 1 || inputChanged;
            if (needPaint) Render();

            if (_running) Osb.Xwin.Cursor.Draw(Osb.Xwin.Mouse.X, Osb.Xwin.Mouse.Y);
            Osb.Xwin.Pit.Sleep(5);
        }

        Osb.Xwin.Cursor.Restore();
        Osb.Xwin.Gfx.Exit();
    }

    public static void Exit() => _running = false;

    private static bool ProcessInput()
    {
        return Desktop.ProcessInput();
    }

    private static void Render()
    {
        Graphics.Clear(Palette.Desktop);
        Desktop.PaintBackground();
        Window.PaintFrames();

        for (int i = 0; i < Osb.Xwin.WindowManager.WindowCount; i++)
        {
            bool vis, min;
            Osb.Xwin.WindowManager.GetWindowInfo(i, out _, out _, out vis, out min, out _);
            if (!vis || min) continue;
            Rect client;
            Window.GetClientArea(i, out client);
            Graphics.FillRect(client.X, client.Y, client.W, client.H, Palette.WindowFace);
            Desktop.PaintWindowClient(i, client.X, client.Y, client.W, client.H);
            Controls.PaintOwner(i);
        }

        Controls.PaintOwner(-1);   // desktop controls (taskbar, etc.)
        Desktop.PaintIcons();
    }
}
