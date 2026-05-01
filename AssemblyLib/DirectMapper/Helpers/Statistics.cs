using System.Diagnostics;
using AsmResolver.DotNet;
using AssemblyLib.Extensions;
using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Helpers;

[Injectable(InjectionType.Singleton)]
public sealed class Statistics(ILogger<Statistics> logger, DataProvider dataProvider)
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
        var types = module.GetAllTypes().Where(t => !t.IsCompilerGenerated());

        var totalTypes = types.Count();
        var totalClasses = types.Count(t => t.IsClass);
        var totalStructs = types.Count(t => t.IsStruct());
        var totalEnums = types.Count(t => t.IsEnum);
        var totalInterfaces = types.Count(t => t.IsInterface);

        var totalObfuscatedClasses = types.Count(t => t.Name is not null && t.IsClass && t.Name.IsObfuscatedName());
        var totalObfuscatedStructs = types.Count(t => t.Name is not null && t.IsStruct() && t.Name.IsObfuscatedName());

        var totalObfuscatedInterfaces = types.Count(t =>
            t.Name is not null && t.IsInterface && t.Name.StartsWith("GInterface")
            || (t.Name?.StartsWith("Interface") ?? false)
        );

        var totalNamedClasses = totalClasses - totalObfuscatedClasses;
        var totalNamedStructs = totalStructs - totalObfuscatedStructs;
        var totalNamedInterfaces = totalInterfaces - totalObfuscatedInterfaces;

        logger.LogInformation("------------- Assembly Statistics ---------------");
        logger.LogInformation("Types:      {Total}", totalTypes);
        logger.LogInformation("Classes:    {Total}", totalClasses);
        logger.LogInformation("Structs:    {Total}", totalStructs);
        logger.LogInformation("Enums:      {Total}", totalEnums);
        logger.LogInformation("Interfaces: {Total}", totalInterfaces);

        logger.LogInformation("---------- De-Obfuscation Statistics -------------");
        logger.LogInformation("Total obfuscated classes:     {Total}", totalObfuscatedClasses);
        logger.LogInformation("Total obfuscated structs:     {Total}", totalObfuscatedStructs);
        logger.LogInformation("Total obfuscated enums:       Cannot be obfuscated");
        logger.LogInformation("Total obfuscated interfaces:  {total}", totalObfuscatedInterfaces);

        logger.LogInformation("Total named classes:          {Total}", totalNamedClasses);
        logger.LogInformation("Total named structs:          {Total}", totalNamedStructs);
        logger.LogInformation("Total named interfaces:       {Total}", totalNamedInterfaces);
        logger.LogInformation("Total named enums:            {total}", totalEnums);

        logger.LogInformation("------------------ Coverage ----------------------");
        logger.LogInformation(
            "Named class coverage:         {coverage}%",
            totalNamedClasses / (float)totalClasses * 100f
        );
        logger.LogInformation(
            "Named struct coverage:        {coverage}%",
            totalNamedStructs / (float)totalStructs * 100f
        );
    }
}
