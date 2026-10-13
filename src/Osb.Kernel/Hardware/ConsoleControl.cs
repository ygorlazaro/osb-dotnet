// Console-level control signals. The IRQ handlers raise them, the shell
// consumes them: Ctrl+C aborts whatever the shell is waiting for.

namespace Osb.Kernel.Hardware;

internal static class ConsoleControl
{
    private static bool _ctrlC;

    // Raised by the keyboard/serial IRQ handlers.
    public static void RequestCtrlC()
    {
        _ctrlC = true;
    }

    // Consumes the flag and returns its previous value.
    public static bool TakeCtrlC()
    {
        bool v = _ctrlC;
        _ctrlC = false;
        return v;
    }
}