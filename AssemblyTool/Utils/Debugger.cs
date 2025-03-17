using System.Diagnostics;
using ReCodeItLib.Utils;

namespace ReCodeItCLI.Utils;

public static class Debugger
{
	[Conditional("WAIT_FOR_DEBUGGER")]
	public static void TryWaitForDebuggerAttach()
	{
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
	}
}