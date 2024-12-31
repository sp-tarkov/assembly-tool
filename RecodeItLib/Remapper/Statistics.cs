using System.Diagnostics;
using ReCodeItLib.Enums;
using ReCodeItLib.Models;
using ReCodeItLib.Utils;

namespace ReCodeItLib.ReMapper;

public class Statistics(
	List<RemapModel> remapModels, 
	Stopwatch stopwatch,
	string outPath,
	string hollowedPath = "")
{
	public void DisplayStatistics(bool validate = false)
	{
		DisplayAlternativeMatches();
		DisplayFailuresAndChanges();
		
		if (!validate)
		{
			DisplayWriteAssembly();
		}
	}

	private void DisplayAlternativeMatches()
	{
		foreach (var remap in remapModels)
		{
			if (remap.Succeeded is false) { continue; }
			
			if (remap.TypeCandidates.Count > 1)
			{
				DisplayAlternativeMatches(remap);
			}
		}
	}
	
	private void DisplayAlternativeMatches(RemapModel remap)
	{
		Logger.Log($"Warning! There were {remap.TypeCandidates.Count()} possible matches for {remap.NewTypeName}. Consider adding more search parameters, Only showing the first 5.", ConsoleColor.Yellow);

		foreach (var type in remap.TypeCandidates.Skip(1).Take(5))
		{
			Logger.Log($"{type.Name}", ConsoleColor.Yellow);
		}
	}

	private void DisplayFailuresAndChanges()
	{
		var failures = 0;
		var changes = 0;
		
		foreach (var remap in remapModels)
		{
			if (remap.Succeeded is false && remap.NoMatchReasons.Contains(ENoMatchReason.AmbiguousWithPreviousMatch))
			{
				Logger.Log("----------------------------------------------------------------------", ConsoleColor.Red);
				Logger.Log("Ambiguous match with a previous match during matching. Skipping remap.", ConsoleColor.Red);
				Logger.Log($"New Type Name: {remap.NewTypeName}", ConsoleColor.Red);
				Logger.Log($"{remap.AmbiguousTypeMatch} already assigned to a previous match.", ConsoleColor.Red);
				Logger.Log("----------------------------------------------------------------------", ConsoleColor.Red);
			}
			else if (remap.Succeeded is false)
			{
				Logger.Log("-----------------------------------------------", ConsoleColor.Red);
				Logger.Log($"Renaming {remap.NewTypeName} failed with reason(s)", ConsoleColor.Red);

				foreach (var reason in remap.NoMatchReasons)
				{
					Logger.Log($"Reason: {reason}", ConsoleColor.Red);
				}

				Logger.Log("-----------------------------------------------", ConsoleColor.Red);
				failures++;
				continue;
			}
			
			changes++;
		}
		
		var renamedColor = changes > 0 ? ConsoleColor.Green : ConsoleColor.Yellow;
		
		Logger.Log($"Renamed {changes} types", renamedColor);
		
		var failColor = failures > 0 ? ConsoleColor.Red : ConsoleColor.Green;
		
		Logger.Log($"Failed to rename {failures} types", failColor);
	}

	private void DisplayWriteAssembly()
	{
		Logger.Log($"Assembly written to `{outPath}`", ConsoleColor.Green);
		Logger.Log($"Hollowed written to `{hollowedPath}`", ConsoleColor.Green);
		Logger.Log($"Remap took {stopwatch.Elapsed.TotalSeconds:F1} seconds", ConsoleColor.Green);
	}
}