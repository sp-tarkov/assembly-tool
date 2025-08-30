using AssemblyLib.AutoMatcher;
using AssemblyLib.ReMapper;
using AssemblyLib.Utils;
using SPTarkov.DI.Annotations;

namespace AssemblyLib;

/// <summary>
///     Provides a communication bridge between the automatch controller and mapping controller
/// </summary>
[Injectable(InjectionType.Singleton)]
public class ControllerBridge(
    AutoMatchController autoMatchController,
    MappingController mappingController,
    DataProvider dataProvider
)
{
    public bool IsRemapperRunning { get; private set; }
    public bool IsAutoMatchRunning { get; private set; }

    public async Task RunRemapper(
        string targetAssemblyPath,
        string? oldAssemblyPath,
        string outPath = "",
        bool validate = false
    )
    {
        if (IsRemapperRunning)
        {
            return;
        }

        IsRemapperRunning = true;
        await mappingController.Run(targetAssemblyPath, oldAssemblyPath, outPath, validate);
        IsRemapperRunning = false;
    }

    public async Task RunAutoMatch(
        string assemblyPath,
        string oldAssemblyPath,
        string oldTypeName,
        string newTypeName,
        bool isRegen
    )
    {
        throw new NotImplementedException();
    }
}
