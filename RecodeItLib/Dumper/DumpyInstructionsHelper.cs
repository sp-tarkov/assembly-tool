using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DumpLib;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace ReCodeItLib.Dumper;

public static class DumpyInstructionsHelper
{
    /// <summary>
    /// <para>Sets up local variables and returns a List of instructions to add.</para>
    /// </summary>
    /// <param name="assembly">AssemblyDefinition</param>
    /// <param name="method">MethodDefinition</param>
    /// <returns>List<Instruction></returns>
    public static List<Instruction> GetBackRequestInstructions(AssemblyDefinition assembly, MethodDefinition method)
    {
        return new List<Instruction>
        {
            Instruction.Create(OpCodes.Ldarg_1),
            Instruction.Create(OpCodes.Ldloc_S, method.Body.Variables[6]),
            Instruction.Create(OpCodes.Call, assembly.MainModule.ImportReference(typeof(DumpLib.DumpyTool).GetMethod("LogRequestResponse", new[] { typeof(object), typeof(object) })))
        };
    }

    /// <summary>
    /// <para>Returns a List of instructions to be added to the method.</para>
    /// <para>This is an Async method so there is two parts, this part and a RunValidation method.</para>
    /// </summary>
    /// <param name="assembly">AssemblyDefinition</param>
    /// <param name="method">MethodDefinition</param>
    /// <returns>List<Instruction></returns>
    public static List<Instruction> GetRunValidationInstructionsMoveNext(AssemblyDefinition assembly, MethodDefinition method)
    {
        // Add our own local variables

        // var1 index0 class1159Type
        var sptClassType = assembly.MainModule.GetTypes().First(DumpyTypeHelper.GetRunValidationType);
        var sptClass = new VariableDefinition(sptClassType);
        method.Body.Variables.Add(sptClass);

        // var2 index1 ExceptionType
        var sptExceptionType = method.Module.ImportReference(typeof(Exception));
        var sptException = new VariableDefinition(sptExceptionType);
        method.Body.Variables.Add(sptException);

        return new List<Instruction>
        {
            // most of this is to keep the Async happy

            Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(OpCodes.Ldfld, assembly.MainModule.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[2]),
            Instruction.Create(OpCodes.Stloc_0),

            // this.Succeed = true;
            Instruction.Create(OpCodes.Ldloc_0),
            Instruction.Create(OpCodes.Ldc_I4_1),
            Instruction.Create(OpCodes.Call, assembly.MainModule.GetTypes().First(DumpyTypeHelper.GetRunValidationType).Methods.First(x => x.Name == "set_Succeed")),

            Instruction.Create(OpCodes.Stloc_1),
            Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(OpCodes.Ldc_I4_S, (sbyte)-2),
            Instruction.Create(OpCodes.Stfld, assembly.MainModule.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[0]),
            Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(OpCodes.Ldflda, assembly.MainModule.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[1]),
            Instruction.Create(OpCodes.Ldloc_1),
            Instruction.Create(OpCodes.Call,
                method.Module.ImportReference(assembly.MainModule.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[1].FieldType.Resolve().Methods.First(x => x.Name == "SetException"))),

            Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(OpCodes.Ldc_I4_S, (sbyte)-2),
            Instruction.Create(OpCodes.Stfld, assembly.MainModule.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[0]),

            Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(OpCodes.Ldflda, assembly.MainModule.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[1]),
            Instruction.Create(OpCodes.Call, method.Module.ImportReference(assembly.MainModule.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[1].FieldType.Resolve().Methods.First(x => x.Name == "SetResult"))),

            Instruction.Create(OpCodes.Ret),
        };
    }

    /// <summary>
    /// <para>Returns a List of instructions to be added to the method.</para>
    /// <para>This is an Async method so there is two parts, this part and a RunValidation method.</para>
    /// </summary>
    /// <param name="assembly">AssemblyDefinition</param>
    /// <param name="method">MethodDefinition</param>
    /// <returns>List<Instruction></returns>
    public static List<Instruction> GetEnsureConsistencyInstructions(AssemblyDefinition oldFileChecker, MethodDefinition method)
    {
        // init local vars
        // var1 index0 TimeSpan type
        var sptTimeSpanType = method.Module.ImportReference(typeof(TimeSpan));
        var sptClass = new VariableDefinition(sptTimeSpanType);
        method.Body.Variables.Add(sptClass);

        // Create genericInstance of a method
        var type = oldFileChecker.MainModule.GetTypes().First(DumpyTypeHelper.GetEnsureConsistencyType).NestedTypes[0].Interfaces[0].InterfaceType;
        var typeMethod = method.Module.ImportReference(typeof(Task).GetMethod("FromResult"));
        var instanceType = new GenericInstanceMethod(typeMethod);
        instanceType.GenericArguments.Add(type);

        return new List<Instruction>
        {
            // return Task.FromResult<ICheckResult>(ConsistencyController.CheckResult.Succeed(default(TimeSpan)));
            Instruction.Create(OpCodes.Ldloca_S, method.Body.Variables[0]),
            Instruction.Create(OpCodes.Initobj, method.Module.ImportReference(typeof(TimeSpan))),
            Instruction.Create(OpCodes.Ldloc_0),
            Instruction.Create(OpCodes.Call, oldFileChecker.MainModule.GetTypes().First(DumpyTypeHelper.GetEnsureConsistencyType).NestedTypes[0].Methods.First(x => x.Name == "Succeed")),
            Instruction.Create(OpCodes.Call, instanceType),
            Instruction.Create(OpCodes.Ret)
        };
    }

    /// <summary>
    /// <para>Returns a List of instructions to be added to the method.</para>
    /// <para>This is an Async method so there is two parts, this part and a MoveNext method.</para>
    /// </summary>
    /// <param name="assembly">AssemblyDefinition</param>
    /// <param name="method">MethodDefinition</param>
    /// <returns>List<Instruction></returns>
    public static List<Instruction> GetRunValidationInstructions(AssemblyDefinition assembly, MethodDefinition method)
    {
        // Create genericInstance of a method
        var type = assembly.MainModule.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0];
        var typeMethod = method.Module.ImportReference(assembly.MainModule.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[1].FieldType.Resolve().Methods.First(x => x.Name == "Start"));
        var instanceMethod = new GenericInstanceMethod(typeMethod);
        instanceMethod.GenericArguments.Add(type);

        return new List<Instruction>
        {
            // <RunValidation>d__.<>t__builder = AsyncTaskMethodBuilder.Create();
            Instruction.Create(OpCodes.Ldloca_S, method.Body.Variables[0]),
            Instruction.Create(OpCodes.Call, method.Module.ImportReference(assembly.MainModule.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[1].FieldType.Resolve().Methods.First(x => x.Name == "Create"))),
            Instruction.Create(OpCodes.Stfld, assembly.MainModule.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[1]),

            // <RunValidation>d__.<>4__this = this;
            Instruction.Create(OpCodes.Ldloca_S, method.Body.Variables[0]),
            Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(OpCodes.Stfld, assembly.MainModule.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[2]),

            // <RunValidation>d__.<>1__state = -1;
            Instruction.Create(OpCodes.Ldloca_S, method.Body.Variables[0]),
            Instruction.Create(OpCodes.Ldc_I4_M1),
            Instruction.Create(OpCodes.Stfld, assembly.MainModule.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[0]),

            // <RunValidation>d__.<>t__builder.Start<Class1159.<RunValidation>d__0>(ref <RunValidation>d__);
            Instruction.Create(OpCodes.Ldloca_S, method.Body.Variables[0]),
            Instruction.Create(OpCodes.Ldflda, assembly.MainModule.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[1]),
            Instruction.Create(OpCodes.Ldloca_S, method.Body.Variables[0]),
            Instruction.Create(OpCodes.Call, instanceMethod),

            // return <RunValidation>d__.<>t__builder.Task;
            Instruction.Create(OpCodes.Ldloca_S, method.Body.Variables[0]),
            Instruction.Create(OpCodes.Ldflda, assembly.MainModule.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[1]),
            Instruction.Create(OpCodes.Call, method.Module.ImportReference(assembly.MainModule.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[1].FieldType.Resolve().Methods.First(x => x.Name == "get_Task"))),
            Instruction.Create(OpCodes.Ret),
        };
    }

    public static List<Instruction> GetDumpyTaskInstructions(AssemblyDefinition oldAssembly, MethodDefinition method)
    {
        return new List<Instruction>
        {
            Instruction.Create(OpCodes.Call, oldAssembly.MainModule.ImportReference(typeof(DumpyTool).GetMethod("StartDumpyTask"))),
            Instruction.Create(OpCodes.Pop)
        };
    }
}