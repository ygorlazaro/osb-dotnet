using System.Runtime;
namespace Osb.Xwin;
internal static unsafe class Program
{
    private static void Main() { }
    [RuntimeExport("xwin_main")]
    private static void XwinMain()
    {
        Arena.Init();
        Osb.Xwin.WindowManager.Init();
        Osb.Toolkit.Controls.Init();
        Osb.Toolkit.Desktop.InitDesktop();
        Osb.Toolkit.App.Run();
        Sys.Exit(0);
        for (;;) { }
    }
}
