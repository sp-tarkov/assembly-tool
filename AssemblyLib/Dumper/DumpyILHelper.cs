using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;

namespace AssemblyLib.Dumper;

public static class DumpyILHelper
{
    /// <summary>
    /// <para>Sets up local variables and returns a List of instructions to add.</para>
    /// </summary>
    /// <param name="gameImporter">Importer</param>
    /// <param name="method">MethodDef</param>
    public static List<CilInstruction> GetBackRequestInstructions(MethodDefinition? method, ReferenceImporter? gameImporter)
    {
        var methodBody = new CilMethodBody(method);
        return new List<CilInstruction>
        {
            new CilInstruction(CilOpCodes.Ldarg_1),
            new CilInstruction(CilOpCodes.Ldloc_S, methodBody.LocalVariables[6]),
            new CilInstruction(CilOpCodes.Call, gameImporter?.ImportMethod(typeof(DumpLib.DumpyTool).GetMethod("LogRequestResponse", new[] { typeof(object), typeof(object) })))
        };
    }

    /// <summary>
    /// <para>Returns a List of instructions to be added to the method.</para>
    /// <para>This is an Async method so there is two parts, this part and a RunValidation method.</para>
    /// </summary>
    /// <param name="gameModule">AssemblyDefinition</param>
    /// <param name="method">MethodDefinition</param>
    public static List<CilInstruction> GetRunValidationInstructionsMoveNext(MethodDefinition? method, ModuleDefinition? gameModule, ModuleDefinition? msModule, ReferenceImporter? gameImporter)
    {
        var methodBody = new CilMethodBody(method);
        // Add our own local variables
        // var1 index0 class1159Type
        var sptClassType = gameModule.GetAllTypes().First(DumpyReflectionHelper.GetRunValidationType);
        var sptClass = new CilLocalVariable(sptClassType.ToTypeSignature());
        methodBody.LocalVariables.Add(sptClass);

        // var2 index1 ExceptionType
        var typer = msModule.GetAllTypes().First(x => x.Name == "Exception");
        var sptExceptionType = gameImporter?.ImportType(typer);
        var sptException = new CilLocalVariable(sptExceptionType.ToTypeSignature());
        methodBody.LocalVariables.Add(sptException);

        return new List<CilInstruction>
        {
            // most of this is to keep the Async happy

            new CilInstruction(CilOpCodes.Ldarg_0),
            new CilInstruction(CilOpCodes.Ldfld, gameModule.GetAllTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0].Fields[2]),
            new CilInstruction(CilOpCodes.Stloc_0),

            // this.Succeed = true;
            new CilInstruction(CilOpCodes.Ldloc_0),
            new CilInstruction(CilOpCodes.Ldc_I4_1),
            new CilInstruction(CilOpCodes.Call, gameModule.GetAllTypes().First(DumpyReflectionHelper.GetRunValidationType).Methods.First(x => x.Name == "set_Succeed")),

            new CilInstruction(CilOpCodes.Stloc_1),
            new CilInstruction(CilOpCodes.Ldarg_0),
            new CilInstruction(CilOpCodes.Ldc_I4_S, (sbyte)-2),
            new CilInstruction(CilOpCodes.Stfld, gameModule.GetAllTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0].Fields[0]),
            new CilInstruction(CilOpCodes.Ldarg_0),
            new CilInstruction(CilOpCodes.Ldflda, gameModule.GetAllTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0].Fields[1]),
            new CilInstruction(CilOpCodes.Ldloc_1),
            new CilInstruction(CilOpCodes.Call,
                gameImporter?.ImportMethod(gameModule.GetAllTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0].Fields[1].DeclaringType.Methods.First(x => x.Name == "SetException"))),

            new CilInstruction(CilOpCodes.Ldarg_0),
            new CilInstruction(CilOpCodes.Ldc_I4_S, (sbyte)-2),
            new CilInstruction(CilOpCodes.Stfld, gameModule.GetAllTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0].Fields[0]),

            new CilInstruction(CilOpCodes.Ldarg_0),
            new CilInstruction(CilOpCodes.Ldflda, gameModule.GetAllTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0].Fields[1]),
            new CilInstruction(CilOpCodes.Call, gameImporter?.ImportMethod(gameModule.GetAllTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0].Fields[1].DeclaringType.Methods.First(x => x.Name == "SetResult"))),

            new CilInstruction(CilOpCodes.Ret),
        };
    }

    /// <summary>
    /// <para>Returns a List of instructions to be added to the method.</para>
    /// <para>This is an Async method so there is two parts, this part and a RunValidation method.</para>
    /// </summary>
    /// <param name="gameModule">AssemblyDefinition</param>
    /// <param name="method">MethodDefinition</param>
    /// <returns>List<Instruction></returns>
    public static List<CilInstruction> GetEnsureConsistencyInstructions(MethodDefinition? method, ModuleDefinition? checkModule, ModuleDefinition? msModule, ReferenceImporter? checkImporter)
    {
        var methodBody = new CilMethodBody(method);
        // init local vars
        // var1 index0 TimeSpan type
        var sptTimeSpanType = checkImporter?.ImportType(msModule.GetAllTypes().First(x => x.Name == "TimeSpan"));
        var sptClass = new CilLocalVariable(sptTimeSpanType.ToTypeSignature());
        methodBody.LocalVariables.Add(sptClass);

        // Create genericInstance of a method
        var type = checkModule.GetAllTypes().First(DumpyReflectionHelper.GetEnsureConsistencyType).NestedTypes[0].Interfaces[0].Interface;
        var typeMethod = checkImporter?.ImportMethod(msModule.GetAllTypes().First(x => x.Name == "Task").Methods.First(x => x.Name == "FromResult"));
        var generac = new MethodSpecification(typeMethod as IMethodDefOrRef, new GenericInstanceMethodSignature(type.ToTypeSignature()));

        return new List<CilInstruction>
        {
            // return Task.FromResult<ICheckResult>(ConsistencyController.CheckResult.Succeed(default(TimeSpan)));
            new CilInstruction(CilOpCodes.Ldloca_S, methodBody.LocalVariables[0]),
            new CilInstruction(CilOpCodes.Initobj, sptTimeSpanType),
            new CilInstruction(CilOpCodes.Ldloc_0),
            new CilInstruction(CilOpCodes.Call, checkModule.GetAllTypes().First(DumpyReflectionHelper.GetEnsureConsistencyType).NestedTypes[0].Methods.First(x => x.Name == "Succeed")),
            new CilInstruction(CilOpCodes.Call, generac),
            new CilInstruction(CilOpCodes.Ret)
        };
    }

    /// <summary>
    /// <para>Returns a List of instructions to be added to the method.</para>
    /// <para>This is an Async method so there is two parts, this part and a MoveNext method.</para>
    /// </summary>
    /// <param name="gameModule">AssemblyDefinition</param>
    /// <param name="method">MethodDefinition</param>
    /// <returns>List<Instruction></returns>
    public static List<CilInstruction> GetRunValidationInstructions(MethodDefinition? method, ModuleDefinition? gameModule, ModuleDefinition? msModule, ReferenceImporter? gameImporter)
    {
        var methodBody = new CilMethodBody(method);
        // Create genericInstance of a method
        var type = gameModule.GetAllTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0];
        var typeMethod = gameImporter?.ImportMethod(msModule.GetAllTypes().First(x => x.Name == "AsyncTaskMethodBuilder").Methods.First(x => x.Name == "Start"));
        var generac = new MethodSpecification(typeMethod as IMethodDefOrRef, new GenericInstanceMethodSignature(type.ToTypeSignature()));

        return new List<CilInstruction>
        {
            // <RunValidation>d__.<>t__builder = AsyncTaskMethodBuilder.Create();
            new CilInstruction(CilOpCodes.Ldloca_S, methodBody.LocalVariables[0]),
            new CilInstruction(CilOpCodes.Call, gameImporter?.ImportMethod(gameModule.GetAllTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0].Fields[1].DeclaringType.Methods.First(x => x.Name == "Create"))),
            new CilInstruction(CilOpCodes.Stfld, gameModule.GetAllTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0].Fields[1]),

            // <RunValidation>dCil__.<>4__this = this;
            new CilInstruction(CilOpCodes.Ldloca_S, methodBody.LocalVariables[0]),
            new CilInstruction(CilOpCodes.Ldarg_0),
            new CilInstruction(CilOpCodes.Stfld, gameModule.GetAllTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0].Fields[2]),

            // <RunValidation>dCil__.<>1__state = -1;
            new CilInstruction(CilOpCodes.Ldloca_S, methodBody.LocalVariables[0]),
            new CilInstruction(CilOpCodes.Ldc_I4_M1),
            new CilInstruction(CilOpCodes.Stfld, gameModule.GetAllTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0].Fields[0]),

            // <RunValidation>dCil__.<>t__builder.Start<Class1159.<RunValidation>d__0>(ref <RunValidation>d__);
            new CilInstruction(CilOpCodes.Ldloca_S, methodBody.LocalVariables[0]),
            new CilInstruction(CilOpCodes.Ldflda, gameModule.GetAllTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0].Fields[1]),
            new CilInstruction(CilOpCodes.Ldloca_S, methodBody.LocalVariables[0]),
            new CilInstruction(CilOpCodes.Call, generac),

            // return <RunValidCilation>d__.<>t__builder.Task;
            new CilInstruction(CilOpCodes.Ldloca_S, methodBody.LocalVariables[0]),
            new CilInstruction(CilOpCodes.Ldflda, gameModule.GetAllTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0].Fields[1]),
            new CilInstruction(CilOpCodes.Call, gameImporter?.ImportMethod(gameModule.GetAllTypes().First(DumpyReflectionHelper.GetRunValidationType).NestedTypes[0].Fields[1].DeclaringType.Methods.First(x => x.Name == "get_Task"))),
            new CilInstruction(CilOpCodes.Ret),
        };
    }

    public static List<CilInstruction> GetDumpyTaskInstructions(MethodDefinition? method, ModuleDefinition? dumpModule, ReferenceImporter? gameImporter)
    {
        return new List<CilInstruction>
        {
            new CilInstruction(CilOpCodes.Call, gameImporter?.ImportMethod(dumpModule.GetAllTypes().First(x => x.Name == "DumpyTool").Methods.First(m => m.Name == "StartDumpyTask"))),
            new CilInstruction(CilOpCodes.Pop)
        };
    }

    public static CilExceptionHandler GetExceptionHandler(MethodDefinition? method, ReferenceImporter? importer, ModuleDefinition? msModule)
    {
        var methodBody = new CilMethodBody(method);
        return new CilExceptionHandler()
        {
            TryStart = new CilInstructionLabel(methodBody.Instructions[3]),
            TryEnd = new CilInstructionLabel(methodBody.Instructions[7]),
            HandlerStart = new CilInstructionLabel(methodBody.Instructions[7]),
            HandlerEnd = new CilInstructionLabel(methodBody.Instructions[16]),
            HandlerType = CilExceptionHandlerType.Exception
        };
    }
}