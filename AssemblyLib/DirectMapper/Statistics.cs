using System.Diagnostics;
using AssemblyLib.Extensions;
using AssemblyLib.Models;
using AssemblyLib.Shared;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper;

[Injectable(InjectionType.Singleton)]
public sealed class Statistics(DataProvider dataProvider)
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

    public void DisplayAssemblyStatistics(string assemblyPath)
    {
        var module = dataProvider.LoadModule(assemblyPath, false);
        var types = module.GetAllTypes();

        var totalTypes = types.Count();
        var totalClasses = types.Count(t => t.IsClass);
        var totalStructs = types.Count(t => t.InheritsFrom("System.ValueType"));
        var totalEnums = types.Count(t => t.IsEnum);
        var totalInterfaces = types.Count(t => t.IsInterface);

        var totalObfuscatedClasses = types.Count(t =>
            t.Name is not null && t.Name.StartsWith("GClass") || (t.Name?.StartsWith("Class") ?? false)
        );

        var totalObfuscatedStructs = types.Count(t =>
            t.Name is not null && t.Name.StartsWith("GStruct") || (t.Name?.StartsWith("Struct") ?? false)
        );

        var totalObfuscatedInterfaces = types.Count(t =>
            t.Name is not null && t.IsInterface && t.Name.StartsWith("GInterface")
            || (t.Name?.StartsWith("Interface") ?? false)
        );

        var totalNamedClasses = totalClasses - totalObfuscatedClasses;
        var totalNamedStructs = totalStructs - totalObfuscatedStructs;
        var totalNamedInterfaces = totalInterfaces - totalObfuscatedInterfaces;

        Log.Information("------------- Assembly Statistics ---------------");
        Log.Information("Types:      {Total}", totalTypes);
        Log.Information("Classes:    {Total}", totalClasses);
        Log.Information("Structs:    {Total}", totalStructs);
        Log.Information("Enums:      {Total}", totalEnums);
        Log.Information("Interfaces: {Total}", totalInterfaces);

        Log.Information("---------- De-Obfuscation Statistics -------------");
        Log.Information("Total obfuscated classes:     {Total}", totalObfuscatedClasses);
        Log.Information("Total obfuscated structs:     {Total}", totalNamedStructs);
        Log.Information("Total obfuscated enums:       Cannot be obfuscated");
        Log.Information("Total obfuscated interfaces:  {total}", totalObfuscatedInterfaces);

        Log.Information("Total named classes:          {Total}", totalNamedClasses);
        Log.Information("Total named structs:          {Total}", totalNamedStructs);
        Log.Information("Total named interfaces:       {Total}", totalNamedInterfaces);
        Log.Information("Total named enums:            {total}", totalEnums);
        Log.Information("Named class coverage:         {coverage}%", totalNamedClasses / (float)totalClasses * 100f);
    }
}
