using System.Runtime;
namespace Osb.Xwin;
internal static unsafe class Program
{
    private static void Main() { }
    [RuntimeExport("xwin_main")]
    private static void XwinMain()
    {
        Arena.Init();
        Desktop.Run();
        Sys.Exit(0);
        for (;;) { }
    }
}
