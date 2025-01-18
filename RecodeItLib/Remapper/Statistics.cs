using System.Diagnostics;
using Newtonsoft.Json;
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
		DisplayFailuresAndChanges(validate);
		
		if (!validate)
		{
			DisplayWriteAssembly();
			
			// In-case a thread is handing 
			Environment.Exit(0);
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
		Logger.LogSync($"Warning! There were {remap.TypeCandidates.Count()} possible matches for {remap.NewTypeName}. Consider adding more search parameters, Only showing the first 5.", ConsoleColor.Yellow);

		foreach (var type in remap.TypeCandidates.Skip(1).Take(5))
		{
			Logger.LogSync($"{type.Name}", ConsoleColor.Yellow);
		}
	}

	private void DisplayFailuresAndChanges(bool validate)
	{
		var failures = 0;
		var changes = 0;
		
		foreach (var remap in remapModels)
		{
			switch (remap.Succeeded)
			{
				case false when remap.NoMatchReasons.Contains(ENoMatchReason.AmbiguousWithPreviousMatch):
					Logger.LogSync("----------------------------------------------------------------------", ConsoleColor.Red);
					Logger.LogSync("Ambiguous match with a previous match during matching. Skipping remap.", ConsoleColor.Red);
					Logger.LogSync($"New Type Name: {remap.NewTypeName}", ConsoleColor.Red);
					Logger.LogSync($"{remap.AmbiguousTypeMatch} already assigned to a previous match.", ConsoleColor.Red);
					Logger.LogSync("----------------------------------------------------------------------", ConsoleColor.Red);
					
					failures++;
					break;
				case false:
				{
					Logger.LogSync("-----------------------------------------------", ConsoleColor.Red);
					Logger.LogSync($"Renaming {remap.NewTypeName} failed with reason(s)", ConsoleColor.Red);

					foreach (var reason in remap.NoMatchReasons)
					{
						Logger.LogSync($"Reason: {reason}", ConsoleColor.Red);
					}

					Logger.LogSync("-----------------------------------------------", ConsoleColor.Red);
					failures++;
					continue;
				}
			}
			
			if (validate && remap.Succeeded)
			{
				Logger.LogSync("Generated Model: ", ConsoleColor.Blue);
				Logger.LogRemapModel(remap);
				
				Logger.LogSync("Passed validation", ConsoleColor.Green);
				return;
			}
			
			changes++;
		}
		
		var renamedColor = changes > 0 ? ConsoleColor.Green : ConsoleColor.Yellow;
		
		Logger.LogSync($"Renamed {changes} types", renamedColor);
		
		var failColor = failures > 0 ? ConsoleColor.Red : ConsoleColor.Green;
		
		Logger.LogSync($"Failed to rename {failures} types", failColor);
	}

	private void DisplayWriteAssembly()
	{
		Logger.LogSync($"Assembly written to `{outPath}`", ConsoleColor.Green);
		Logger.LogSync($"Hollowed written to `{hollowedPath}`", ConsoleColor.Green);
		Logger.LogSync($"Remap took {stopwatch.Elapsed.TotalSeconds:F1} seconds", ConsoleColor.Green);
	}
}