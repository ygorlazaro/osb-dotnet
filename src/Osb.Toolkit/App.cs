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

        // Layers, bottom to top: desktop icons, windows, desktop controls,
        // then the deskbar with the menu / shutdown dialog over everything.
        Desktop.PaintIcons();

        // Painter's algorithm: walk z from the bottom and finish each window
        // (frame, client, controls) before the next one. Drawing all frames
        // first and all clients afterwards (in array order) lets a lower
        // window's client paint over a higher window's frame and client.
        int count = Osb.Xwin.WindowManager.WindowCount;
        for (int z = 0; z < count; z++)
        {
            int i = Osb.Xwin.WindowManager.IndexAtZ(z);
            if (i < 0) continue;

            bool vis, min;
            Osb.Xwin.WindowManager.GetWindowInfo(i, out _, out _, out vis, out min, out _);
            if (!vis || min) continue;

            Osb.Xwin.WindowManager.PaintFrame(i);   // also fills the client background
            Rect client;
            Window.GetClientArea(i, out client);
            Desktop.PaintWindowClient(i, client.X, client.Y, client.W, client.H);
            Controls.PaintOwner(i);
        }

        Controls.PaintOwner(-1);    // desktop controls (screen coordinates)
        Desktop.PaintBackground();  // deskbar on top of windows, then menu / dialog
    }
}
