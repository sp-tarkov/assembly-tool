using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Dumper;

[Injectable]
public class DumpyILHelper(DumperCilHelper dumperCilHelper, DumpyReflectionHelper dumpyReflectionHelper)
{
    /// <summary>
    /// <para>Sets up local variables and returns a List of instructions to add.</para>
    /// </summary>
    /// <param name="gameImporter">Importer</param>
    /// <param name="method">MethodDef</param>
    public List<CilInstruction> GetBackRequestInstructions(MethodDefinition? method, ReferenceImporter? gameImporter)
    {
        return new List<CilInstruction>
        {
            new(CilOpCodes.Ldarg_1),
            new(CilOpCodes.Ldloc_S, method.CilMethodBody.LocalVariables[6]),
            new(CilOpCodes.Call, dumperCilHelper.BackRequestLogRequestResponseMethod(gameImporter)),
        };
    }

    /// <summary>
    /// <para>Returns a List of instructions to be added to the method.</para>
    /// <para>This is an Async method so there is two parts, this part and a RunValidation method.</para>
    /// </summary>
    /// <param name="gameModule">AssemblyDefinition</param>
    /// <param name="method">MethodDefinition</param>
    public List<CilInstruction> GetRunValidationInstructionsMoveNext(
        MethodDefinition? method,
        ModuleDefinition? gameModule,
        ModuleDefinition? msModule,
        ReferenceImporter? gameImporter
    )
    {
        // Add our own local variables
        // var1 index0 class1159Type
        var sptClassType = gameModule.GetAllTypes().First(dumpyReflectionHelper.GetRunValidationType);
        var sptClass = new CilLocalVariable(sptClassType.ToTypeSignature());
        method.CilMethodBody.LocalVariables.Add(sptClass);

        // var2 index1 ExceptionType
        var typer = msModule.GetAllTypes().First(x => x.Name == "Exception");
        var sptExceptionType = gameImporter?.ImportType(typer);
        var sptException = new CilLocalVariable(sptExceptionType.ToTypeSignature());
        method.CilMethodBody.LocalVariables.Add(sptException);

        return new List<CilInstruction>
        {
            // most of this is to keep the Async happy

            new(CilOpCodes.Ldarg_0),
            new(CilOpCodes.Ldfld, dumperCilHelper.MoveNextValidationFieldTwo(gameModule)),
            new(CilOpCodes.Stloc_0),
            // this.Succeed = true;
            new(CilOpCodes.Ldloc_0),
            new(CilOpCodes.Ldc_I4_1),
            new(CilOpCodes.Call, dumperCilHelper.MoveNextValidationSetSucceedMethod(gameModule)),
            new(CilOpCodes.Stloc_1),
            new(CilOpCodes.Ldarg_0),
            new(CilOpCodes.Ldc_I4_S, (sbyte)-2),
            new(CilOpCodes.Stfld, dumperCilHelper.MoveNextValidationFieldZero(gameModule)),
            new(CilOpCodes.Ldarg_0),
            new(CilOpCodes.Ldflda, dumperCilHelper.MoveNextValidationFieldOne(gameModule)),
            new(CilOpCodes.Ldloc_1),
            new(CilOpCodes.Call, dumperCilHelper.MoveNextValidationSetExceptionMethod(gameImporter, gameModule)),
            new(CilOpCodes.Ldarg_0),
            new(CilOpCodes.Ldc_I4_S, (sbyte)-2),
            new(CilOpCodes.Stfld, dumperCilHelper.MoveNextValidationFieldZero(gameModule)),
            new(CilOpCodes.Ldarg_0),
            new(CilOpCodes.Ldflda, dumperCilHelper.MoveNextValidationFieldOne(gameModule)),
            new(CilOpCodes.Call, dumperCilHelper.MoveNextValidationSetResultMethod(gameImporter, gameModule)),
            new(CilOpCodes.Ret),
        };
    }

    /// <summary>
    /// <para>Returns a List of instructions to be added to the method.</para>
    /// <para>This is an Async method so there is two parts, this part and a RunValidation method.</para>
    /// </summary>
    /// <param name="gameModule">AssemblyDefinition</param>
    /// <param name="method">MethodDefinition</param>
    /// <returns>List<Instruction></returns>
    public List<CilInstruction> GetEnsureConsistencyInstructions(
        MethodDefinition? method,
        ModuleDefinition? checkModule,
        ModuleDefinition? msModule,
        ReferenceImporter? checkImporter
    )
    {
        // init local vars
        // var1 index0 TimeSpan type
        var sptTimeSpanType = checkImporter?.ImportType(msModule.GetAllTypes().First(x => x.Name == "TimeSpan"));
        var sptClass = new CilLocalVariable(sptTimeSpanType.ToTypeSignature());
        method.CilMethodBody.LocalVariables.Add(sptClass);

        // Create genericInstance of a method
        var type = checkModule
            .GetAllTypes()
            .First(dumpyReflectionHelper.GetEnsureConsistencyType)
            .NestedTypes[0]
            .Interfaces[0]
            .Interface;
        var typeMethod = checkImporter?.ImportMethod(
            msModule.GetAllTypes().First(x => x.Name == "Task").Methods.First(x => x.Name == "FromResult")
        );
        var generac = new MethodSpecification(
            typeMethod as IMethodDefOrRef,
            new GenericInstanceMethodSignature(type.ToTypeSignature())
        );

        return new List<CilInstruction>
        {
            // return Task.FromResult<ICheckResult>(ConsistencyController.CheckResult.Succeed(default(TimeSpan)));
            new(CilOpCodes.Ldloca_S, method.CilMethodBody.LocalVariables[0]),
            new(CilOpCodes.Initobj, sptTimeSpanType),
            new(CilOpCodes.Ldloc_0),
            new(CilOpCodes.Call, dumperCilHelper.EnsureConsistencySucceedMethod(checkModule)),
            new(CilOpCodes.Call, generac),
            new(CilOpCodes.Ret),
        };
    }

    /// <summary>
    /// <para>Returns a List of instructions to be added to the method.</para>
    /// <para>This is an Async method so there is two parts, this part and a MoveNext method.</para>
    /// </summary>
    /// <param name="gameModule">AssemblyDefinition</param>
    /// <param name="method">MethodDefinition</param>
    /// <returns>List<Instruction></returns>
    public List<CilInstruction> GetRunValidationInstructions(
        MethodDefinition? method,
        ModuleDefinition? gameModule,
        ModuleDefinition? msModule,
        ReferenceImporter? gameImporter
    )
    {
        // Create genericInstance of a method
        var type = gameModule.GetAllTypes().First(dumpyReflectionHelper.GetRunValidationType).NestedTypes[0];
        var typeMethod = gameImporter?.ImportMethod(
            msModule.GetAllTypes().First(x => x.Name == "AsyncTaskMethodBuilder").Methods.First(x => x.Name == "Start")
        );
        var generac = new MethodSpecification(
            typeMethod as IMethodDefOrRef,
            new GenericInstanceMethodSignature(type.ToTypeSignature())
        );

        return new List<CilInstruction>
        {
            // <RunValidation>d__.<>t__builder = AsyncTaskMethodBuilder.Create();
            new(CilOpCodes.Ldloca_S, method.CilMethodBody.LocalVariables[0]),
            new(CilOpCodes.Call, dumperCilHelper.RunValidationCreateMethod(gameImporter, gameModule)),
            new(CilOpCodes.Stfld, dumperCilHelper.RunValidationFieldOne(gameModule)),
            // <RunValidation>dCil__.<>4__this = this;
            new(CilOpCodes.Ldloca_S, method.CilMethodBody.LocalVariables[0]),
            new(CilOpCodes.Ldarg_0),
            new(CilOpCodes.Stfld, dumperCilHelper.RunValidationFieldTwo(gameModule)),
            // <RunValidation>dCil__.<>1__state = -1;
            new(CilOpCodes.Ldloca_S, method.CilMethodBody.LocalVariables[0]),
            new(CilOpCodes.Ldc_I4_M1),
            new(CilOpCodes.Stfld, dumperCilHelper.RunValidationFieldZero(gameModule)),
            // <RunValidation>dCil__.<>t__builder.Start<Class1159.<RunValidation>d__0>(ref <RunValidation>d__);
            new(CilOpCodes.Ldloca_S, method.CilMethodBody.LocalVariables[0]),
            new(CilOpCodes.Ldflda, dumperCilHelper.RunValidationFieldOne(gameModule)),
            new(CilOpCodes.Ldloca_S, method.CilMethodBody.LocalVariables[0]),
            new(CilOpCodes.Call, generac),
            // return <RunValidCilation>d__.<>t__builder.Task;
            new(CilOpCodes.Ldloca_S, method.CilMethodBody.LocalVariables[0]),
            new(CilOpCodes.Ldflda, dumperCilHelper.RunValidationFieldOne(gameModule)),
            new(CilOpCodes.Call, dumperCilHelper.RunValidationGetTaskMethod(gameImporter, gameModule)),
            new(CilOpCodes.Ret),
        };
    }

    public List<CilInstruction> GetDumpyTaskInstructions(
        MethodDefinition? method,
        ModuleDefinition? dumpModule,
        ReferenceImporter? gameImporter
    )
    {
        return new List<CilInstruction>
        {
            new CilInstruction(
                CilOpCodes.Call,
                gameImporter?.ImportMethod(
                    dumpModule
                        .GetAllTypes()
                        .First(x => x.Name == "DumpyTool")
                        .Methods.First(m => m.Name == "StartDumpyTask")
                )
            ),
            new CilInstruction(CilOpCodes.Pop),
        };
    }

    public CilExceptionHandler GetExceptionHandler(
        MethodDefinition? method,
        ReferenceImporter? importer,
        ModuleDefinition? msModule
    )
    {
        return new CilExceptionHandler
        {
            HandlerType = CilExceptionHandlerType.Exception,
            TryStart = method.CilMethodBody.Instructions[3].CreateLabel(),
            TryEnd = method.CilMethodBody.Instructions[7].CreateLabel(),
            HandlerStart = method.CilMethodBody.Instructions[7].CreateLabel(),
            HandlerEnd = method.CilMethodBody.Instructions[16].CreateLabel(),
            ExceptionType = importer.ImportType(msModule.GetAllTypes().First(x => x.Name == "Exception")),
        };
    }
}
