using AsmResolver.PE.DotNet.Cil;
using AssemblyLib.Patching;
using AssemblyLib.Patching.MemberLookup;
using EFT;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Patches.Fixes;

[Injectable]
public class ShowIncompatibleNotificationPatch(MemberLookup lookup, DataProvider dataProvider) : IModulePatch
{
    public bool Enabled => true;

    public void Patch()
    {
        var targetMethod = lookup.Eft.Method<Player.FirearmController.Idling>("ShowIncompatibleNotification");
        if (targetMethod is null)
        {
            throw new FailedToFindTypeException(
                "Could not find `EFT.Player.FirearmController.Idling.ShowIncompatibleNotification()` when patching"
            );
        }

        var module = dataProvider.LoadedModule!;

        // player_0 exists on FirearmOperation, the base type for Idling
        var player0Field = lookup.Eft.Field<Player.FirearmController.FirearmOperation>("player_0");
        if (player0Field is null)
        {
            throw new FailedToFindTypeException("Could not find player_0 field");
        }

        var isYourPlayerGetter = lookup.Eft.Property<Player>("IsYourPlayer")?.GetMethod;
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
    }
}
