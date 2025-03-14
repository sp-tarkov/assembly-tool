using System.Diagnostics;
using Newtonsoft.Json;
using ReCodeItLib.Enums;
using ReCodeItLib.Models;
using ReCodeItLib.Utils;

namespace ReCodeItLib.ReMapper;

public class Statistics(
	List<RemapModel> remapModels, 
	Stopwatch stopwatch,
	string outPath)
{
	public int TypePublicizedCount;
	public int FieldPublicizedCount;
	public int PropertyPublicizedCount;
	public int MethodPublicizedCount;

	public int FieldRenamedCount;
	public int PropertyRenamedCount;
	public int MethodRenamedCount;
	
	private string _hollowedPath = string.Empty;
	
	public void DisplayStatistics(bool validate = false, string hollowedPath = "")
	{
		_hollowedPath = hollowedPath;
		
		DisplayAlternativeMatches();
		DisplayFailuresAndChanges(validate);

		if (validate) return;
		
		DisplayWriteAssembly();
			
		// In-case a thread is hanging 
		Environment.Exit(0);
	}

	private void DisplayAlternativeMatches()
	{
		Logger.LogSync("--------------------------------------------------");
		
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
		
		Logger.LogSync("--------------------------------------------------");
		Logger.LogSync($"Types publicized: {TypePublicizedCount}", ConsoleColor.Green);
		Logger.LogSync($"Types renamed: {changes}", ConsoleColor.Green);
		
		if (failures > 0)
		{
			Logger.LogSync($"Types that failed: {failures}", ConsoleColor.Red);
		}
		
		Logger.LogSync($"Methods publicized: {MethodPublicizedCount}", ConsoleColor.Green);
		Logger.LogSync($"Methods renamed: {MethodRenamedCount}", ConsoleColor.Green);
		Logger.LogSync($"Fields publicized: {FieldPublicizedCount}", ConsoleColor.Green);
		Logger.LogSync($"Fields renamed: {FieldRenamedCount}", ConsoleColor.Green);
		Logger.LogSync($"Properties publicized: {PropertyPublicizedCount}", ConsoleColor.Green);
		Logger.LogSync($"Properties renamed: {PropertyRenamedCount}", ConsoleColor.Green);
	}

	private void DisplayWriteAssembly()
	{
		Logger.LogSync("--------------------------------------------------");
		
		Logger.LogSync($"Assembly written to `{outPath}`", ConsoleColor.Green);
		Logger.LogSync($"Hollowed written to `{_hollowedPath}`", ConsoleColor.Green);
		Logger.LogSync($"Remap took {stopwatch.Elapsed.TotalSeconds:F1} seconds", ConsoleColor.Green);
	}
}