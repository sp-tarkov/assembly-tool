using AsmResolver.DotNet;
using AssemblyLib.Models;
using AssemblyLib.Models.Interfaces;
using Serilog;

namespace AssemblyLib.AutoMatcher.Filters;

public abstract class AbstractAutoMatchFilter : IAutoMatchFilter
{
    public abstract bool Filter(TypeDefinition target, TypeDefinition candidate, SearchParams searchParams);

    /// <summary>
    ///     Logs the failure to the console
    /// </summary>
    /// <param name="failureReason">Reason the filter failed</param>
    /// <returns>False</returns>
    protected static bool LogFailure(string failureReason)
    {
        Log.Error("{failureReason}", failureReason);
        return false;
    }
}

public interface IAutoMatchFilter
{
    bool Filter(TypeDefinition target, TypeDefinition candidate, SearchParams searchParams);
}