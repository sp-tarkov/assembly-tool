using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Helpers;

[Injectable]
public class PatchHelper(ILogger<PatchHelper> logger, DataProvider dataProvider)
{
    /// <summary>
    ///     Nukes all instructions from a void type method
    /// </summary>
    public void NukeVoidBody(CilMethodBody methodBody)
    {
        methodBody.Instructions.Clear();
        methodBody.Instructions.Add(CilOpCodes.Ret);
    }

    /// <summary>
    ///     Nukes all instructions from a Task method. Does NOT work on Task<T>
    /// </summary>
    /// <param name="methodBody"></param>
    public void NukeTaskBody(CilMethodBody methodBody)
    {
        methodBody.Instructions.Clear();
        methodBody.Instructions.Add(new CilInstruction(CilOpCodes.Call, GetTaskCompletedTaskRef()));
        methodBody.Instructions.Add(CilOpCodes.Ret);
        methodBody.MaxStack = 1;
    }

    /// <summary>
    ///     Nukes all instructions from a bool method, returns a constant true or false
    /// </summary>
    public void NukeBoolBody(CilMethodBody methodBody, bool returnValue = false)
    {
        methodBody.Instructions.Clear();
        methodBody.Instructions.Add(returnValue ? CilOpCodes.Ldc_I4_1 : CilOpCodes.Ldc_I4_0);
        methodBody.Instructions.Add(CilOpCodes.Ret);
        methodBody.MaxStack = 1;
    }

    /// <summary>
    ///     Nukes all instructions from an int/short/byte/char method, returns a constant value
    /// </summary>
    public void NukeInt32Body(CilMethodBody methodBody, int returnValue = 0)
    {
        methodBody.Instructions.Clear();
        methodBody.Instructions.Add(new CilInstruction(CilOpCodes.Ldc_I4, returnValue));
        methodBody.Instructions.Add(CilOpCodes.Ret);
        methodBody.MaxStack = 1;
    }

    /// <summary>
    ///     Nukes all instructions from a long method, returns a constant value
    /// </summary>
    public void NukeInt64Body(CilMethodBody methodBody, long returnValue = 0L)
    {
        methodBody.Instructions.Clear();
        methodBody.Instructions.Add(new CilInstruction(CilOpCodes.Ldc_I8, returnValue));
        methodBody.Instructions.Add(CilOpCodes.Ret);
        methodBody.MaxStack = 1;
    }

    /// <summary>
    ///     Nukes all instructions from a float method, returns a constant value
    /// </summary>
    public void NukeFloatBody(CilMethodBody methodBody, float returnValue = 0f)
    {
        methodBody.Instructions.Clear();
        methodBody.Instructions.Add(new CilInstruction(CilOpCodes.Ldc_R4, returnValue));
        methodBody.Instructions.Add(CilOpCodes.Ret);
        methodBody.MaxStack = 1;
    }

    /// <summary>
    ///     Nukes all instructions from a double method, returns a constant value
    /// </summary>
    public void NukeDoubleBody(CilMethodBody methodBody, double returnValue = 0.0)
    {
        methodBody.Instructions.Clear();
        methodBody.Instructions.Add(new CilInstruction(CilOpCodes.Ldc_R8, returnValue));
        methodBody.Instructions.Add(CilOpCodes.Ret);
        methodBody.MaxStack = 1;
    }

    /// <summary>
    ///     Nukes all instructions from a string method, returns a constant string or null
    /// </summary>
    public void NukeStringBody(CilMethodBody methodBody, string? returnValue = null)
    {
        methodBody.Instructions.Clear();
        if (returnValue is null)
        {
            methodBody.Instructions.Add(CilOpCodes.Ldnull);
        }
        else
        {
            methodBody.Instructions.Add(new CilInstruction(CilOpCodes.Ldstr, returnValue));
        }
        methodBody.Instructions.Add(CilOpCodes.Ret);
        methodBody.MaxStack = 1;
    }

    /// <summary>
    ///     Nukes all instructions from a reference type method, returns null
    /// </summary>
    public void NukeRefTypeBody(CilMethodBody methodBody)
    {
        methodBody.Instructions.Clear();
        methodBody.Instructions.Add(CilOpCodes.Ldnull);
        methodBody.Instructions.Add(CilOpCodes.Ret);
        methodBody.MaxStack = 1;
    }

    /// <summary>
    ///     Nukes all instructions from a value type (struct) method, returns default(T)
    ///     via initobj — works for any struct including custom ones
    /// </summary>
    public void NukeValueTypeBody(CilMethodBody methodBody, ITypeDefOrRef valueType)
    {
        var module = dataProvider.LoadedModule!;
        var imported = module.DefaultImporter.ImportType(valueType);

        methodBody.Instructions.Clear();

        // Allocate a local for the struct, zero it, and return it
        var local = new CilLocalVariable(imported.ToTypeSignature(dataProvider.Context));
        methodBody.LocalVariables.Add(local);

        methodBody.Instructions.Add(new CilInstruction(CilOpCodes.Ldloca, local));
        methodBody.Instructions.Add(new CilInstruction(CilOpCodes.Initobj, imported));
        methodBody.Instructions.Add(new CilInstruction(CilOpCodes.Ldloc, local));
        methodBody.Instructions.Add(CilOpCodes.Ret);
        methodBody.MaxStack = 1;
    }

    /// <summary>
    ///     Automatically infers the correct nuke method from the method's return type signature
    /// </summary>
    public void NukeBody(MethodDefinition method)
    {
        var body = method.CilMethodBody!;
        var returnType = method.Signature!.ReturnType;

        switch (returnType)
        {
            case { FullName: "System.Void" }:
                NukeVoidBody(body);
                break;

            case { FullName: "System.Boolean" }:
                NukeBoolBody(body);
                break;

            case { FullName: "System.Int32" }
            or { FullName: "System.Int16" }
            or { FullName: "System.Byte" }
            or { FullName: "System.SByte" }
            or { FullName: "System.UInt16" }
            or { FullName: "System.UInt32" }
            or { FullName: "System.Char" }:
                NukeInt32Body(body);
                break;

            case { FullName: "System.Int64" }
            or { FullName: "System.UInt64" }:
                NukeInt64Body(body);
                break;

            case { FullName: "System.Single" }:
                NukeFloatBody(body);
                break;

            case { FullName: "System.Double" }:
                NukeDoubleBody(body);
                break;

            case { FullName: "System.String" }:
                NukeStringBody(body);
                break;

            case { FullName: "System.Threading.Tasks.Task" }:
                NukeTaskBody(body);
                break;

            case TypeDefOrRefSignature { IsValueType: true } valueSig:
                NukeValueTypeBody(body, valueSig.Type);
                break;

            // Covers classes, interfaces, arrays, generics — anything reference-y
            default:
                NukeRefTypeBody(body);
                break;
        }
    }

    public bool NukeType(TypeDefinition typeDef)
    {
        var module = dataProvider.LoadedModule;
        var fullName = typeDef.FullName;

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Attempting to destroy type {TypeName}", fullName);
        }

        // Remove nested type from parent, or top-level type from module
        if (typeDef.IsNested)
        {
            var parent = typeDef.DeclaringType!;
            if (!parent.NestedTypes.Remove(typeDef))
            {
                logger.LogWarning("Failed to remove nested type {TypeName} from {Parent}", fullName, parent.FullName);
                return false;
            }
        }
        else
        {
            if (!module!.TopLevelTypes.Remove(typeDef))
            {
                logger.LogWarning("Failed to remove top-level type {TypeName} from module", fullName);
                return false;
            }
        }

        logger.LogInformation("Destroyed type {TypeName}", fullName);
        return true;
    }

    public int NukeTypes(IEnumerable<TypeDefinition> types)
    {
        var toDestroy = types.ToList();
        var destroyed = 0;

        foreach (var type in toDestroy)
        {
            if (NukeType(type))
            {
                destroyed++;
            }
        }

        logger.LogInformation("Destroyed {Count}/{Total} types", destroyed, toDestroy.Count);
        return destroyed;
    }

    /// <summary>
    ///     Nop's a range of instructions
    /// </summary>
    /// <param name="instructions">Instructions to NOP in</param>
    /// <param name="start">Start index, inclusive</param>
    /// <param name="end">End index, inclusive</param>
    public void NopRange(CilInstructionCollection instructions, int start, int end)
    {
        for (var i = start; i <= end; i++)
        {
            instructions[i].OpCode = CilOpCodes.Nop;
            instructions[i].Operand = null;
        }
    }

    /// <summary>
    ///     Gets a reference to Task.CompletedTask
    /// </summary>
    private IMethodDefOrRef GetTaskCompletedTaskRef()
    {
        var taskTypeRef = new TypeReference(
            dataProvider.LoadedModule!.CorLibTypeFactory.CorLibScope,
            "System.Threading.Tasks",
            "Task"
        );

        var completedTaskGetter = new MemberReference(
            taskTypeRef,
            "get_CompletedTask",
            MethodSignature.CreateStatic(taskTypeRef.ToTypeSignature(dataProvider.Context))
        );

        return dataProvider.LoadedModule.DefaultImporter.ImportMethod(completedTaskGetter);
    }
}
