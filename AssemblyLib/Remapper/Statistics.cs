using System.Diagnostics;
using AssemblyLib.Enums;
using AssemblyLib.Models;
using AssemblyLib.Utils;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.ReMapper;

[Injectable(InjectionType.Singleton)]
public sealed class Statistics(
	DataProvider dataProvider
	)
{
	public int TypePublicizedCount;
	public int FieldPublicizedCount;
	public int PropertyPublicizedCount;
	public int MethodPublicizedCount;

	public int FieldRenamedCount;
	public int PropertyRenamedCount;
	public int MethodRenamedCount;
	
	private string _hollowedPath = string.Empty;
	
	public readonly Stopwatch Stopwatch = new();
	
	public void DisplayStatistics(bool validate = false, string hollowedPath = "", string outPath = "")
	{
		_hollowedPath = hollowedPath;
		
		DisplayAlternativeMatches();
		DisplayFailuresAndChanges(validate);

		if (validate) return;
		
		DisplayWriteAssembly(outPath);
			
		if (dataProvider.Settings.CopyToGame && !string.IsNullOrEmpty(dataProvider.Settings.GamePath) && File.Exists(outPath))
		{
			var gameDest = Path.Combine(dataProvider.Settings.GamePath, "EscapeFromTarkov_Data", "Managed", "Assembly-CSharp.dll");
                
			File.Copy(outPath, gameDest, true);
                
			Log.Information("Assembly has been installed to the game: {GameDest}", gameDest);
		}
		
		if (dataProvider.Settings.CopyToModules && !string.IsNullOrEmpty(dataProvider.Settings.ModulesProjectPath) && File.Exists(hollowedPath))
		{
			var hollowedDest = Path.Combine(dataProvider.Settings.ModulesProjectPath, "project", "Shared", "Hollowed", "hollowed.dll");
                
			File.Copy(hollowedPath, hollowedDest, true);
                
			Log.Information("Hollowed has been copied to the modules project: {HollowedDest}", hollowedDest);
		}
		
		// In-case a thread is hanging 
		Environment.Exit(0);
	}

	private void DisplayAlternativeMatches()
	{
		Log.Information("\n--------------------------------------------------");
		
		foreach (var remap in dataProvider.GetRemaps())
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
		Log.Information("Warning! There were {TypeCandidatesCount} possible matches for {RemapNewTypeName}. Consider adding more search parameters, Only showing the first 5.", 
			remap.TypeCandidates.Count, 
			remap.NewTypeName
			);

		foreach (var type in remap.TypeCandidates.Skip(1).Take(5))
		{
			Log.Warning("{Utf8String}", type.Name?.ToString());
		}
	}

	public bool DisplayFailuresAndChanges(bool validate, bool isRemapProcess = false)
	{
		var failures = 0;
		var changes = 0;
		
		foreach (var remap in dataProvider.GetRemaps())
		{
			switch (remap.Succeeded)
			{
				case false when remap.NoMatchReasons.Contains(ENoMatchReason.AmbiguousWithPreviousMatch):
					Log.Error("----------------------------------------------------------------------");
					Log.Error("Ambiguous match with a previous match during matching. Skipping remap.");
					Log.Error($"New Type Name: {remap.NewTypeName}");
					Log.Error($"{remap.AmbiguousTypeMatch} already assigned to a previous match.");
					Log.Error("----------------------------------------------------------------------");
					
					failures++;
					break;
				case false:
				{
					Log.Error("-----------------------------------------------");
					Log.Error($"Renaming {remap.NewTypeName} failed with reason(s)");

					foreach (var reason in remap.NoMatchReasons)
					{
						Log.Error($"Reason: {reason}", ConsoleColor.Red);
					}

					Log.Error("-----------------------------------------------", ConsoleColor.Red);
					failures++;
					continue;
				}
			}
			
			if (validate && remap.Succeeded)
			{
				//Log.Error("Generated Model: ");
				//Log.Error(remap);
				
				Log.Information("Passed validation");
				return failures == 0;
			}
			
			changes++;
		}
		
		var succeeded = failures == 0;
		
		Log.Information("--------------------------------------------------");
		Log.Information("Types publicized: {S}", TypePublicizedCount);
		Log.Information("Types renamed: {Changes}", changes);
		Log.Information("--------------------------------------------------");
		
		if (failures > 0)
		{
			Log.Error("Types that failed: {Failures}", failures);
			return succeeded;
		}
		
		if (isRemapProcess) return succeeded;
		
		Log.Information("Methods publicized: {S}", MethodPublicizedCount);
		Log.Information("Methods renamed: {S}", MethodRenamedCount);
		Log.Information("Fields publicized: {S}", FieldPublicizedCount);
		Log.Information("Fields renamed: {S}", FieldRenamedCount);
		Log.Information("Properties publicized: {S}", PropertyPublicizedCount);
		Log.Information("Properties renamed: {S}", PropertyRenamedCount);

		return succeeded;
	}

	private void DisplayWriteAssembly(string outPath)
	{
		Log.Information("--------------------------------------------------");
		
		Log.Information("Assembly written to `{OutPath}`", outPath);
		Log.Information("Hollowed written to `{HollowedPath}`", _hollowedPath);
		
		dataProvider.UpdateMappingFile();
		
		Log.Information("Remap took {ElapsedTotalSeconds:F1} seconds", Stopwatch.Elapsed.TotalSeconds);
	}
}