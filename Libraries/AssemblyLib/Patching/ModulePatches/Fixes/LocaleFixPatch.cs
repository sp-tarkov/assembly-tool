using AsmResolver.DotNet;
using AsmResolver.DotNet.Serialized;
using AsmResolver.PE.DotNet.Cil;
using EFT;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Patching.ModulePatches.Fixes;

[Injectable]
public class LocaleFixPatch(MemberLookup.MemberLookup lookup, DataProvider dataProvider) : IModulePatch
{
    public bool Enabled => true;

    /// <summary>
    ///     There are times when the locale handling code is called while there is already
    /// partial locale data in place. This patch removes a check that stops
    /// locale data being loaded from the server if any locale data already exists.
    /// </summary>
    public void Patch()
    {
        var body = lookup.Eft.Method<EftCreateProfileOperation.CG_Struct0>("MoveNext")?.CilMethodBody;
        if (body is null)
        {
            throw new FailedToFindTypeException(
                "Could not find `Eft.CreateProfileOperation.CG_Struct0.MoveNext()` when patching"
            );
        }

        var containsCultureMethod = lookup.Eft.Method<LocalizationManager>("ContainsCulture");
        if (containsCultureMethod is null)
        {
            throw new FailedToFindTypeException(
                "Could not find `EFT.LocalizationManager.ContainsCulture()` when patching"
            );
        }

        var instructions = body.Instructions;

        var searchIndex = -1;
        for (var i = 0; i < instructions.Count; i++)
        {
            if (
                instructions[i].OpCode == CilOpCodes.Callvirt
                && instructions[i].Operand is SerializedMethodDefinition smd
                && dataProvider.Context.ResolveMethod(smd, smd.DeclaringModule, out var definition)
                    == ResolutionStatus.Success
                && definition == containsCultureMethod
            )
            {
                searchIndex = i - 3;
                break;
            }
        }

        if (searchIndex != -1)
        {
            // ldloc (this for ContainsCulture)
            instructions[searchIndex].OpCode = CilOpCodes.Nop;
            instructions[searchIndex].Operand = null;

            // ldloc (this for get_Culture)
            instructions[searchIndex + 1].OpCode = CilOpCodes.Nop;
            instructions[searchIndex + 1].Operand = null;

            // callvirt get_Culture
            instructions[searchIndex + 2].OpCode = CilOpCodes.Nop;
            instructions[searchIndex + 2].Operand = null;

            // replace ContainsCulture with false
            instructions[searchIndex + 3].OpCode = CilOpCodes.Ldc_I4_0;
            instructions[searchIndex + 3].Operand = null;

            body.Instructions.OptimizeMacros();
            return;
        }

        Log.Error("LocaleFixPatch Failed");
    }
}
