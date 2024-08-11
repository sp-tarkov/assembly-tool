using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using DumpLib;
using ReCodeIt.Utils;

namespace ReCodeItLib.Dumper;

public static class DumpyInstructionsHelper
{
    /// <summary>
    /// <para>Sets up local variables and returns a List of instructions to add.</para>
    /// </summary>
    /// <param name="assembly">AssemblyDefinition</param>
    /// <param name="method">MethodDefinition</param>
    /// <returns>List<Instruction></returns>
    public static List<Instruction> GetBackRequestInstructions(ModuleDefMD assembly, MethodDef method)
    {
        var importer = new Importer(assembly);

        return new List<Instruction>
        {
            Instruction.Create(OpCodes.Ldarg_1),
            Instruction.Create(OpCodes.Ldloc_S, method.Body.Variables[6]),
            Instruction.Create(OpCodes.Call, importer.Import(typeof(DumpLib.DumpyTool).GetMethod("LogRequestResponse", new[] { typeof(object), typeof(object) })))
        };
    }

    /// <summary>
    /// <para>Returns a List of instructions to be added to the method.</para>
    /// <para>This is an Async method so there is two parts, this part and a RunValidation method.</para>
    /// </summary>
    /// <param name="assembly">AssemblyDefinition</param>
    /// <param name="method">MethodDefinition</param>
    /// <returns>List<Instruction></returns>
    public static List<Instruction> GetRunValidationInstructionsMoveNext(ModuleDefMD assembly, MethodDef method)
    {
        // TODO: [CWX] TRIED CHANGING OPTIONS
        var importer = new Importer(assembly, ImporterOptions.TryToUseExistingAssemblyRefs);
        var test = ModuleDefMD.Load("C:\\Battlestate Games\\Escape from Tarkov\\EscapeFromTarkov_Data\\Managed\\mscorlib.dll");

        // Add our own local variables

        // var1 index0 class1159Type
        var sptClassType = assembly.GetTypes().First(DumpyTypeHelper.GetRunValidationType);
        var sptClass = new Local(sptClassType.ToTypeSig());
        method.Body.Variables.Add(sptClass);

        // var2 index1 ExceptionType
        // TODO: [CWX] this is the problem, Exception is being imported via System.Private.CoreLib... Needs to come from MsCorLib in EFT managed Dir
        var typer = test.GetTypes().First(x => x.Name.ToLower() == "exception");
        var sptExceptionType = importer.Import(typer);
        var sptException = new Local(sptExceptionType.ToTypeSig());
        method.Body.Variables.Add(sptException);

        return new List<Instruction>
        {
            // most of this is to keep the Async happy

            Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(OpCodes.Ldfld, assembly.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[2]),
            Instruction.Create(OpCodes.Stloc_0),

            // this.Succeed = true;
            Instruction.Create(OpCodes.Ldloc_0),
            Instruction.Create(OpCodes.Ldc_I4_1),
            Instruction.Create(OpCodes.Call, assembly.GetTypes().First(DumpyTypeHelper.GetRunValidationType).Methods.First(x => x.Name == "set_Succeed")),

            Instruction.Create(OpCodes.Stloc_1),
            Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(OpCodes.Ldc_I4_S, (sbyte)-2),
            Instruction.Create(OpCodes.Stfld, assembly.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[0]),
            Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(OpCodes.Ldflda, assembly.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[1]),
            Instruction.Create(OpCodes.Ldloc_1),
            Instruction.Create(OpCodes.Call,
                importer.Import(assembly.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[1].FieldType.ScopeType.ResolveTypeDef().Methods.First(x => x.Name == "SetException"))),

            Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(OpCodes.Ldc_I4_S, (sbyte)-2),
            Instruction.Create(OpCodes.Stfld, assembly.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[0]),

            Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(OpCodes.Ldflda, assembly.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[1]),
            Instruction.Create(OpCodes.Call, importer.Import(assembly.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[1].FieldType.ScopeType.ResolveTypeDef().Methods.First(x => x.Name == "SetResult"))),

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
    public static List<Instruction> GetEnsureConsistencyInstructions(ModuleDefMD assembly, ModuleDefMD fileChecker, MethodDef method)
    {
        var importer = new Importer(assembly);
        var test = ModuleDefMD.Load("C:\\Battlestate Games\\Escape from Tarkov\\EscapeFromTarkov_Data\\Managed\\mscorlib.dll");
        
        // init local vars
        // var1 index0 TimeSpan type
        var sptTimeSpanType = importer.Import(test.GetTypes().First(x => x.Name == "TimeSpan"));
        var sptClass = new Local(sptTimeSpanType.ToTypeSig());
        method.Body.Variables.Add(sptClass);

        // Create genericInstance of a method
        var type = fileChecker.GetTypes().First(DumpyTypeHelper.GetEnsureConsistencyType).NestedTypes[0].Interfaces[0].Interface;
        var typeMethod = importer.Import(test.GetTypes().First(x => x.Name == "Task").Methods.First(x => x.Name == "FromResult"));
        var generac = new MethodSpecUser(typeMethod as IMethodDefOrRef, new GenericInstMethodSig(type.ToTypeSig()));

        return new List<Instruction>
        {
            // return Task.FromResult<ICheckResult>(ConsistencyController.CheckResult.Succeed(default(TimeSpan)));
            Instruction.Create(OpCodes.Ldloca_S, method.Body.Variables[0]),
            Instruction.Create(OpCodes.Initobj, sptTimeSpanType),
            Instruction.Create(OpCodes.Ldloc_0),
            Instruction.Create(OpCodes.Call, fileChecker.GetTypes().First(DumpyTypeHelper.GetEnsureConsistencyType).NestedTypes[0].Methods.First(x => x.Name == "Succeed")),
            Instruction.Create(OpCodes.Call, generac),
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
    public static List<Instruction> GetRunValidationInstructions(ModuleDefMD assembly, MethodDef method)
    {
        var importer = new Importer(assembly);
        var test = ModuleDefMD.Load("C:\\Battlestate Games\\Escape from Tarkov\\EscapeFromTarkov_Data\\Managed\\mscorlib.dll");

        // Create genericInstance of a method
        var type = assembly.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0];
        var typeMethod = importer.Import(test.GetTypes().First(x => x.Name == "AsyncTaskMethodBuilder").Methods.First(x => x.Name == "Start"));
        var generac = new MethodSpecUser(typeMethod as IMethodDefOrRef, new GenericInstMethodSig(type.ToTypeSig()));

        return new List<Instruction>
        {
            // <RunValidation>d__.<>t__builder = AsyncTaskMethodBuilder.Create();
            Instruction.Create(OpCodes.Ldloca_S, method.Body.Variables[0]),
            Instruction.Create(OpCodes.Call, importer.Import(assembly.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[1].FieldType.ScopeType.ResolveTypeDef().Methods.First(x => x.Name == "Create"))),
            Instruction.Create(OpCodes.Stfld, assembly.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[1]),

            // <RunValidation>d__.<>4__this = this;
            Instruction.Create(OpCodes.Ldloca_S, method.Body.Variables[0]),
            Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(OpCodes.Stfld, assembly.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[2]),

            // <RunValidation>d__.<>1__state = -1;
            Instruction.Create(OpCodes.Ldloca_S, method.Body.Variables[0]),
            Instruction.Create(OpCodes.Ldc_I4_M1),
            Instruction.Create(OpCodes.Stfld, assembly.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[0]),

            // <RunValidation>d__.<>t__builder.Start<Class1159.<RunValidation>d__0>(ref <RunValidation>d__);
            Instruction.Create(OpCodes.Ldloca_S, method.Body.Variables[0]),
            Instruction.Create(OpCodes.Ldflda, assembly.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[1]),
            Instruction.Create(OpCodes.Ldloca_S, method.Body.Variables[0]),
            Instruction.Create(OpCodes.Call, generac),

            // return <RunValidation>d__.<>t__builder.Task;
            Instruction.Create(OpCodes.Ldloca_S, method.Body.Variables[0]),
            Instruction.Create(OpCodes.Ldflda, assembly.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[1]),
            Instruction.Create(OpCodes.Call, importer.Import(assembly.GetTypes().First(DumpyTypeHelper.GetRunValidationType).NestedTypes[0].Fields[1].FieldType.ScopeType.ResolveTypeDef().Methods.First(x => x.Name == "get_Task"))),
            Instruction.Create(OpCodes.Ret),
        };
    }

    public static List<Instruction> GetDumpyTaskInstructions(ModuleDefMD assembly, MethodDef method)
    {
        var importer = new Importer(assembly);
        
        return new List<Instruction>
        {
            Instruction.Create(OpCodes.Call, importer.Import(typeof(DumpyTool).GetMethod("StartDumpyTask"))),
            Instruction.Create(OpCodes.Pop)
        };
    }
}