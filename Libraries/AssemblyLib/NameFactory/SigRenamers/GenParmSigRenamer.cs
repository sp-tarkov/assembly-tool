using AsmResolver.DotNet;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.NameFactory.SigRenamers;

[Injectable]
public class GenParmSigRenamer(ILogger<PropertySigRenamer> logger) : ISigRenamer
{
    public int Priority => 0;
    public bool Enabled => true;
    public ERenamerType Type => ERenamerType.GenericParameters;

    public void Rename(TypeDefinition targetType, TypeDefinition dummyType)
    {
        RenameGenericParametersOnMethods(targetType, dummyType);
        if (!targetType.HasGenericParameters || targetType.GenericParameters.Count != dummyType.GenericParameters.Count)
        {
            return;
        }

        for (var i = 0; i < targetType.GenericParameters.Count; i++)
        {
            var targetGenericParameter = targetType.GenericParameters[i];
            var dummyGenericParameter = dummyType.GenericParameters[i];

            if (targetGenericParameter.Name?.ToString() == dummyGenericParameter.Name?.ToString())
            {
                continue;
            }

            var oldName = targetGenericParameter.Name?.ToString();

            targetGenericParameter.Name = dummyGenericParameter.Name;

            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Renamed generic param: {old} -> {new}",
                    oldName,
                    targetGenericParameter.Name?.ToString()
                );
            }
        }
    }

    private void RenameGenericParametersOnMethods(TypeDefinition targetType, TypeDefinition dummyType)
    {
        foreach (var targetMethod in targetType.Methods)
        {
            var dummyMethod = dummyType.Methods.FirstOrDefault(m => m.Name == targetMethod.Name);

            if (
                dummyMethod is null
                || !targetMethod.HasGenericParameters
                || targetMethod.GenericParameters.Count != dummyMethod.GenericParameters.Count
            )
            {
                continue;
            }

            for (var i = 0; i < targetMethod.GenericParameters.Count; i++)
            {
                var targetGenericParameter = targetMethod.GenericParameters[i];
                var dummyGenericParameter = dummyMethod.GenericParameters[i];

                if (targetGenericParameter.Name?.ToString() == dummyGenericParameter.Name?.ToString())
                {
                    continue;
                }

                var oldName = targetGenericParameter.Name?.ToString();

                targetGenericParameter.Name = dummyGenericParameter.Name;

                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug(
                        "Renamed method generic param: {old} -> {new}",
                        oldName,
                        targetGenericParameter.Name?.ToString()
                    );
                }
            }
        }
    }
}
