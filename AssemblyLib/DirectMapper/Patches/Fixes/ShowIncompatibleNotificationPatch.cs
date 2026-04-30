using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssemblyLib.DirectMapper.Helpers;
using AssemblyLib.Exceptions;
using AssemblyLib.Shared;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Patches.Fixes;

[Injectable]
public class ShowIncompatibleNotificationPatch(PatchHelper patchHelper, DataProvider dataProvider) : IPatch
{
    public bool Enabled => true;

    public void Patch()
    {
        var targetMethod = dataProvider
            .LoadedModule!.GetAllTypes()
            .FirstOrDefault(t => t.Namespace == "EFT" && t.Name == "Player")
            ?.NestedTypes.FirstOrDefault(t => t.Name == "FirearmController")
            ?.NestedTypes.FirstOrDefault(t => t.Name == "Idling")
            ?.Methods.FirstOrDefault(m => m.Name == "ShowIncompatibleNotification");

        if (targetMethod is null)
        {
            throw new FailedToFindTypeException(
                "Could not find `EFT.Player.FirearmController.Idling.ShowIncompatibleNotification()` when patching"
            );
        }

        var declaringType = targetMethod.DeclaringType!;
        var module = dataProvider.LoadedModule!;

        if (declaringType.BaseType?.Resolve(dataProvider.Context, out var baseType) != ResolutionStatus.Success)
        {
            throw new FailedToFindTypeException("Could not resolve Idling base type");
        }

        var player0Field = baseType?.Fields.FirstOrDefault(f => f.Name == "player_0");
        if (player0Field is null)
        {
            throw new FailedToFindTypeException("Could not find player_0 field");
        }

        if (player0Field.Signature!.FieldType is not TypeDefOrRefSignature playerFieldSig)
        {
            throw new FailedToFindTypeException("player_0 type is not TypeDefOrRefSignature");
        }

        if (playerFieldSig.Type.Resolve(dataProvider.Context, out var playerFieldDef) != ResolutionStatus.Success)
        {
            throw new FailedToFindTypeException("Could not resolve playerFieldSig");
        }

        var isYourPlayerGetter = playerFieldDef?.Properties.FirstOrDefault(p => p.Name == "IsYourPlayer")?.GetMethod;
        if (isYourPlayerGetter is null)
        {
            throw new FailedToFindTypeException("Could not find IsYourPlayer getter");
        }

        // ALWAYS use the imported version when making references!!!!
        var importedPlayerField = module.DefaultImporter.ImportField(player0Field);
        var importedIsYourPlayerGetter = module.DefaultImporter.ImportMethod(isYourPlayerGetter);

        var instructions = targetMethod.CilMethodBody!.Instructions;
        var originalFirst = instructions[0];

        instructions.Insert(0, new CilInstruction(CilOpCodes.Ldarg_0));
        instructions.Insert(1, new CilInstruction(CilOpCodes.Ldfld, importedPlayerField));
        instructions.Insert(2, new CilInstruction(CilOpCodes.Callvirt, importedIsYourPlayerGetter));
        instructions.Insert(3, new CilInstruction(CilOpCodes.Brtrue_S, originalFirst.CreateLabel()));
        instructions.Insert(4, new CilInstruction(CilOpCodes.Ret));
        instructions.CalculateOffsets();

        Log.Information("ShowIncompatibleNotificationPatch Successful");
    }
}
