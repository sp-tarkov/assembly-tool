using AsmResolver.DotNet;
using AssemblyLib.Models.Interfaces;
using Serilog;

namespace AssemblyLib.AutoMatcher.Filters;

public abstract class AbstractAutoMatchFilter : IAutoMatchFilter
{
    public abstract bool Filter(TypeDefinition target, TypeDefinition candidate, IFilterParams filterParam);

    /// <summary>
    ///     Logs the failure to the console
    /// </summary>
    /// <param name="failed">Did the filter fail</param>
    /// <param name="failureReason">Reason the filter failed</param>
    /// <returns>False if failed</returns>
    protected static bool LogFailureOrContinue(bool failed, string failureReason = "")
    {
        if (failed)
        {
            Log.Error("{failureReason}", failureReason);
        }
        
        return failed;
    }
}

public interface IAutoMatchFilter;