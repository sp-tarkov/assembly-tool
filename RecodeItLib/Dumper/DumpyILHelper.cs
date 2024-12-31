using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace ReCodeItLib.Dumper;

public static class DumpyILHelper
{
    /// <summary>
    /// <para>Sets up local variables and returns a List of instructions to add.</para>
    /// </summary>
    /// <param name="gameImporter">Importer</param>
    /// <param name="method">MethodDef</param>
    public static List<Instruction> GetBackRequestInstructions(MethodDef method, Importer? gameImporter)
    {
        return new List<Instruction>
        {
            Instruction.Create(OpCodes.Ldarg_1),
            Instruction.Create(OpCodes.Ldloc_S, method.Body.Variables[6]),
            Instruction.Create(OpCodes.Call, gameImporter?.Import(typeof(DumpLib.DumpyTool).GetMethod("LogRequestResponse", new[] { typeof(object), typeof(object) })))
        };
    }

    /// <summary>
    /// <para>Returns a List of instructions to be added to the method.</para>
    /// <para>This is an Async method so there is two parts, this part and a RunValidation method.</para>
    /// </summary>
    /// <param name="gameModule">AssemblyDefinition</param>
    /// <param name="method">MethodDefinition</param>
    public static List<Instruction> GetRunValidationInstructionsMoveNext(MethodDef method, ModuleDefMD gameModule, ModuleDefMD msModule, Importer? gameImporter)
    {
        // Add our own local variables
        // var1 index0 class1159Type
        var sptClassType = gameModule.GetTypes().First(DumpyReflectionHelper.GetRunValidationType);
        var sptClass = new Local(sptClassType.ToTypeSig());
        method.Body.Variables.Add(sptClass);

        // var2 index1 ExceptionType
        var typer = msModule.GetTypes().First(x => x.Name.ToLower() == "exception");
        var sptExceptionType = gameImporter?.Import(typer);
        var sptException = new Local(sptExceptionType.ToTypeSig());
        method.Body.Variables.Add(sptException);

        return new List<Instruction>
        {
            // most of this is to keep the Async happy

            Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(OpCodes.Ldfld, gameModule.GetTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0].Fields[2]),
            Instruction.Create(OpCodes.Stloc_0),

            // this.Succeed = true;
            Instruction.Create(OpCodes.Ldloc_0),
            Instruction.Create(OpCodes.Ldc_I4_1),
            Instruction.Create(OpCodes.Call, gameModule.GetTypes().First(DumpyReflectionHelper.GetRunValidationType).Methods.First(x => x.Name == "set_Succeed")),

            Instruction.Create(OpCodes.Stloc_1),
            Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(OpCodes.Ldc_I4_S, (sbyte)-2),
            Instruction.Create(OpCodes.Stfld, gameModule.GetTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0].Fields[0]),
            Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(OpCodes.Ldflda, gameModule.GetTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0].Fields[1]),
            Instruction.Create(OpCodes.Ldloc_1),
            Instruction.Create(OpCodes.Call,
                gameImporter?.Import(gameModule.GetTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0].Fields[1].FieldType.ScopeType.ResolveTypeDef().Methods.First(x => x.Name == "SetException"))),

            Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(OpCodes.Ldc_I4_S, (sbyte)-2),
            Instruction.Create(OpCodes.Stfld, gameModule.GetTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0].Fields[0]),

            Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(OpCodes.Ldflda, gameModule.GetTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0].Fields[1]),
            Instruction.Create(OpCodes.Call, gameImporter?.Import(gameModule.GetTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0].Fields[1].FieldType.ScopeType.ResolveTypeDef().Methods.First(x => x.Name == "SetResult"))),

            Instruction.Create(OpCodes.Ret),
        };
    }

    /// <summary>
    /// <para>Returns a List of instructions to be added to the method.</para>
    /// <para>This is an Async method so there is two parts, this part and a RunValidation method.</para>
    /// </summary>
    /// <param name="gameModule">AssemblyDefinition</param>
    /// <param name="method">MethodDefinition</param>
    /// <returns>List<Instruction></returns>
    public static List<Instruction> GetEnsureConsistencyInstructions(MethodDef method, ModuleDefMD checkModule, ModuleDefMD msModule, Importer? checkImporter)
    {
        // init local vars
        // var1 index0 TimeSpan type
        var sptTimeSpanType = checkImporter?.Import(msModule.GetTypes().First(x => x.Name == "TimeSpan"));
        var sptClass = new Local(sptTimeSpanType.ToTypeSig());
        method.Body.Variables.Add(sptClass);

        // Create genericInstance of a method
        var type = checkModule.GetTypes().First(DumpyReflectionHelper.GetEnsureConsistencyType).NestedTypes[0].Interfaces[0].Interface;
        var typeMethod = checkImporter?.Import(msModule.GetTypes().First(x => x.Name == "Task").Methods.First(x => x.Name == "FromResult"));
        var generac = new MethodSpecUser(typeMethod as IMethodDefOrRef, new GenericInstMethodSig(type.ToTypeSig()));

        return new List<Instruction>
        {
            // return Task.FromResult<ICheckResult>(ConsistencyController.CheckResult.Succeed(default(TimeSpan)));
            Instruction.Create(OpCodes.Ldloca_S, method.Body.Variables[0]),
            Instruction.Create(OpCodes.Initobj, sptTimeSpanType),
            Instruction.Create(OpCodes.Ldloc_0),
            Instruction.Create(OpCodes.Call, checkModule.GetTypes().First(DumpyReflectionHelper.GetEnsureConsistencyType).NestedTypes[0].Methods.First(x => x.Name == "Succeed")),
            Instruction.Create(OpCodes.Call, generac),
            Instruction.Create(OpCodes.Ret)
        };
    }

    /// <summary>
    /// <para>Returns a List of instructions to be added to the method.</para>
    /// <para>This is an Async method so there is two parts, this part and a MoveNext method.</para>
    /// </summary>
    /// <param name="gameModule">AssemblyDefinition</param>
    /// <param name="method">MethodDefinition</param>
    /// <returns>List<Instruction></returns>
    public static List<Instruction> GetRunValidationInstructions(MethodDef method, ModuleDefMD gameModule, ModuleDefMD msModule, Importer? gameImporter)
    {
        // Create genericInstance of a method
        var type = gameModule.GetTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0];
        var typeMethod = gameImporter?.Import(msModule.GetTypes().First(x => x.Name == "AsyncTaskMethodBuilder").Methods.First(x => x.Name == "Start"));
        var generac = new MethodSpecUser(typeMethod as IMethodDefOrRef, new GenericInstMethodSig(type.ToTypeSig()));

        return new List<Instruction>
        {
            // <RunValidation>d__.<>t__builder = AsyncTaskMethodBuilder.Create();
            Instruction.Create(OpCodes.Ldloca_S, method.Body.Variables[0]),
            Instruction.Create(OpCodes.Call, gameImporter?.Import(gameModule.GetTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0].Fields[1].FieldType.ScopeType.ResolveTypeDef().Methods.First(x => x.Name == "Create"))),
            Instruction.Create(OpCodes.Stfld, gameModule.GetTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0].Fields[1]),

            // <RunValidation>d__.<>4__this = this;
            Instruction.Create(OpCodes.Ldloca_S, method.Body.Variables[0]),
            Instruction.Create(OpCodes.Ldarg_0),
            Instruction.Create(OpCodes.Stfld, gameModule.GetTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0].Fields[2]),

            // <RunValidation>d__.<>1__state = -1;
            Instruction.Create(OpCodes.Ldloca_S, method.Body.Variables[0]),
            Instruction.Create(OpCodes.Ldc_I4_M1),
            Instruction.Create(OpCodes.Stfld, gameModule.GetTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0].Fields[0]),

            // <RunValidation>d__.<>t__builder.Start<Class1159.<RunValidation>d__0>(ref <RunValidation>d__);
            Instruction.Create(OpCodes.Ldloca_S, method.Body.Variables[0]),
            Instruction.Create(OpCodes.Ldflda, gameModule.GetTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0].Fields[1]),
            Instruction.Create(OpCodes.Ldloca_S, method.Body.Variables[0]),
            Instruction.Create(OpCodes.Call, generac),

            // return <RunValidation>d__.<>t__builder.Task;
            Instruction.Create(OpCodes.Ldloca_S, method.Body.Variables[0]),
            Instruction.Create(OpCodes.Ldflda, gameModule.GetTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0].Fields[1]),
            Instruction.Create(OpCodes.Call, gameImporter?.Import(gameModule.GetTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0].Fields[1].FieldType.ScopeType.ResolveTypeDef().Methods.First(x => x.Name == "get_Task"))),
            Instruction.Create(OpCodes.Ret),
        };
    }

    public static List<Instruction> GetDumpyTaskInstructions(MethodDef method, ModuleDefMD dumpModule, Importer? gameImporter)
    {
        return new List<Instruction>
        {
            Instruction.Create(OpCodes.Call, gameImporter?.Import(dumpModule.GetTypes().First(x => x.Name == "DumpyTool").Methods.First(m => m.Name == "StartDumpyTask"))),
            Instruction.Create(OpCodes.Pop)
        };
    }

    public static ExceptionHandler GetExceptionHandler(MethodDef method, Importer? importer, ModuleDefMD msModule)
    {
        return new ExceptionHandler()
        {
            TryStart = method.Body.Instructions[3],
            TryEnd = method.Body.Instructions[7],
            HandlerStart = method.Body.Instructions[7],
            HandlerEnd = method.Body.Instructions[16],
            CatchType = importer?.Import(msModule.GetTypes().First(x => x.Name == "Exception"))
        };
    }
}