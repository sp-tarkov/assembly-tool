//#define WAIT_FOR_DEBUGGER

using AssemblyLib.Utils;

namespace AssemblyTool.Utils;

public static class Debugger
{
    public static void TryWaitForDebuggerAttach()
    {
#if WAIT_FOR_DEBUGGER
        const int maxDots = 3;
        var dotCount = 0;

        while (!System.Diagnostics.Debugger.IsAttached)
        {
            var dots = new string('.', dotCount);

            Console.Clear();
            Logger.Log($"Waiting for debugger{dots}");

            dotCount = (dotCount + 1) % (maxDots + 1);
            Thread.Sleep(500);
        }
#endif
    }
}
