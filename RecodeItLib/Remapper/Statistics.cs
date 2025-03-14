using System.Diagnostics;
using Newtonsoft.Json;
using ReCodeItLib.Application;
using ReCodeItLib.Enums;
using ReCodeItLib.Models;
using ReCodeItLib.Utils;

namespace ReCodeItLib.ReMapper;

internal sealed class Statistics(List<RemapModel> remapModels) 
	: IComponent
{
	public int TypePublicizedCount;
	public int FieldPublicizedCount;
	public int PropertyPublicizedCount;
	public int MethodPublicizedCount;

	public int FieldRenamedCount;
	public int PropertyRenamedCount;
	public int MethodRenamedCount;
	
	private string _hollowedPath = string.Empty;
	
	public void DisplayStatistics(bool validate = false, string hollowedPath = "", string outPath = "")
	{
		_hollowedPath = hollowedPath;
		
		DisplayAlternativeMatches();
		DisplayFailuresAndChanges(validate);

		if (validate) return;
		
		DisplayWriteAssembly(outPath);
			
		if (DataProvider.Settings.CopyToGame && !string.IsNullOrEmpty(DataProvider.Settings.GamePath) && File.Exists(outPath))
		{
			var gameDest = Path.Combine(DataProvider.Settings.GamePath, "EscapeFromTarkov_Data", "Managed", "Assembly-CSharp.dll");
                
			File.Copy(outPath, gameDest, true);
                
			Logger.Log($"Assembly has been installed to the game: {gameDest}", ConsoleColor.Yellow);
		}
		
		if (DataProvider.Settings.CopyToModules && !string.IsNullOrEmpty(DataProvider.Settings.ModulesProjectPath) && File.Exists(hollowedPath))
		{
			var hollowedDest = Path.Combine(DataProvider.Settings.ModulesProjectPath, "project", "Shared", "Hollowed", "hollowed.dll");
                
			File.Copy(hollowedPath, hollowedDest, true);
                
			Logger.Log($"Hollowed has been copied to the modules project: {hollowedDest}", ConsoleColor.Yellow);
		}
		
		// In-case a thread is hanging 
		Environment.Exit(0);
	}

	private void DisplayAlternativeMatches()
	{
		Logger.Log("\n--------------------------------------------------");
		
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

	private void DisplayFailuresAndChanges(bool validate)
	{
		var failures = 0;
		var changes = 0;
		
		foreach (var remap in remapModels)
		{
			switch (remap.Succeeded)
			{
				case false when remap.NoMatchReasons.Contains(ENoMatchReason.AmbiguousWithPreviousMatch):
					Logger.Log("----------------------------------------------------------------------", ConsoleColor.Red);
					Logger.Log("Ambiguous match with a previous match during matching. Skipping remap.", ConsoleColor.Red);
					Logger.Log($"New Type Name: {remap.NewTypeName}", ConsoleColor.Red);
					Logger.Log($"{remap.AmbiguousTypeMatch} already assigned to a previous match.", ConsoleColor.Red);
					Logger.Log("----------------------------------------------------------------------", ConsoleColor.Red);
					
					failures++;
					break;
				case false:
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
			}
			
			if (validate && remap.Succeeded)
			{
				Logger.Log("Generated Model: ", ConsoleColor.Blue);
				Logger.LogRemapModel(remap);
				
				Logger.Log("Passed validation", ConsoleColor.Green);
				return;
			}
			
			changes++;
		}
		
		Logger.Log("--------------------------------------------------");
		Logger.Log($"Types publicized: {TypePublicizedCount}", ConsoleColor.Green);
		Logger.Log($"Types renamed: {changes}", ConsoleColor.Green);
		
		if (failures > 0)
		{
			Logger.Log($"Types that failed: {failures}", ConsoleColor.Red);
		}
		
		Logger.Log($"Methods publicized: {MethodPublicizedCount}", ConsoleColor.Green);
		Logger.Log($"Methods renamed: {MethodRenamedCount}", ConsoleColor.Green);
		Logger.Log($"Fields publicized: {FieldPublicizedCount}", ConsoleColor.Green);
		Logger.Log($"Fields renamed: {FieldRenamedCount}", ConsoleColor.Green);
		Logger.Log($"Properties publicized: {PropertyPublicizedCount}", ConsoleColor.Green);
		Logger.Log($"Properties renamed: {PropertyRenamedCount}", ConsoleColor.Green);
	}

	private void DisplayWriteAssembly(string outPath)
	{
		Logger.Log("--------------------------------------------------");
		
		Logger.Log($"Assembly written to `{outPath}`", ConsoleColor.Green);
		Logger.Log($"Hollowed written to `{_hollowedPath}`", ConsoleColor.Green);
		
		if (DataProvider.Settings.MappingPath != string.Empty)
		{
			DataProvider.UpdateMapping(DataProvider.Settings.MappingPath.Replace("mappings.", "mappings-new."), remapModels);
		}
		
		Logger.Log($"Remap took {Logger.Stopwatch.Elapsed.TotalSeconds:F1} seconds", ConsoleColor.Green);
	}
}