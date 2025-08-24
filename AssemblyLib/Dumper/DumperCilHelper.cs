using AsmResolver.DotNet;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Dumper;

[Injectable]
public class DumperCilHelper(DumpyReflectionHelper dumpyReflectionHelper)
{
    public IMethodDefOrRef RunValidationCreateMethod(
        ReferenceImporter importer,
        ModuleDefinition gameModule
    )
    {
        var typeToFind = gameModule
            .GetAllTypes()
            .FirstOrDefault(dumpyReflectionHelper.GetRunValidationType);
        var methodToFind = typeToFind
            .NestedTypes[0]
            .Fields[1]
            .Signature.FieldType.Resolve()
            .Methods.FirstOrDefault(x => x.Name == "Create");
        return importer.ImportMethod(methodToFind);
    }

    public IMethodDefOrRef RunValidationGetTaskMethod(
        ReferenceImporter importer,
        ModuleDefinition gameModule
    )
    {
        var typeToFind = gameModule
            .GetAllTypes()
            .FirstOrDefault(dumpyReflectionHelper.GetRunValidationType);
        var methodToFind = typeToFind
            .NestedTypes[0]
            .Fields[1]
            .Signature.FieldType.Resolve()
            .Methods.FirstOrDefault(x => x.Name == "get_Task");
        return importer.ImportMethod(methodToFind);
    }

    public FieldDefinition RunValidationFieldZero(ModuleDefinition gameModule)
    {
        var typeToFind = gameModule
            .GetAllTypes()
            .FirstOrDefault(dumpyReflectionHelper.GetRunValidationType);
        var fieldToFind = typeToFind.NestedTypes[0].Fields[0];
        return fieldToFind;
    }

    public FieldDefinition RunValidationFieldOne(ModuleDefinition gameModule)
    {
        var typeToFind = gameModule
            .GetAllTypes()
            .FirstOrDefault(dumpyReflectionHelper.GetRunValidationType);
        var fieldToFind = typeToFind.NestedTypes[0].Fields[1];
        return fieldToFind;
    }

    public FieldDefinition RunValidationFieldTwo(ModuleDefinition gameModule)
    {
        var typeToFind = gameModule
            .GetAllTypes()
            .FirstOrDefault(dumpyReflectionHelper.GetRunValidationType);
        var fieldToFind = typeToFind.NestedTypes[0].Fields[2];
        return fieldToFind;
    }

    public IMethodDescriptor BackRequestLogRequestResponseMethod(ReferenceImporter importer)
    {
        var methodToFind = typeof(DumpLib.DumpyTool).GetMethod(
            "LogRequestResponse",
            new[] { typeof(object), typeof(object) }
        );
        return importer.ImportMethod(methodToFind);
    }

    public MethodDefinition MoveNextValidationSetSucceedMethod(ModuleDefinition gameModule)
    {
        var typeToFind = gameModule
            .GetAllTypes()
            .FirstOrDefault(dumpyReflectionHelper.GetRunValidationType);
        var methodToFind = typeToFind.Methods.FirstOrDefault(x => x.Name == "set_Succeed");
        return methodToFind;
    }

    public FieldDefinition MoveNextValidationFieldZero(ModuleDefinition gameModule)
    {
        var typeToFind = gameModule
            .GetAllTypes()
            .FirstOrDefault(dumpyReflectionHelper.GetRunValidationType);
        var fieldToFind = typeToFind.NestedTypes[0].Fields[0];
        return fieldToFind;
    }

    public FieldDefinition MoveNextValidationFieldOne(ModuleDefinition gameModule)
    {
        var typeToFind = gameModule
            .GetAllTypes()
            .FirstOrDefault(dumpyReflectionHelper.GetRunValidationType);
        var fieldToFind = typeToFind.NestedTypes[0].Fields[1];
        return fieldToFind;
    }

    public FieldDefinition MoveNextValidationFieldTwo(ModuleDefinition gameModule)
    {
        var typeToFind = gameModule
            .GetAllTypes()
            .FirstOrDefault(dumpyReflectionHelper.GetRunValidationType);
        var fieldToFind = typeToFind.NestedTypes[0].Fields[2];
        return fieldToFind;
    }

    public IMethodDefOrRef MoveNextValidationSetExceptionMethod(
        ReferenceImporter importer,
        ModuleDefinition gameModule
    )
    {
        var typeToFind = gameModule
            .GetAllTypes()
            .FirstOrDefault(dumpyReflectionHelper.GetRunValidationType);
        var methodToFind = typeToFind
            .NestedTypes[0]
            .Fields[1]
            .Signature.FieldType.Resolve()
            .Methods.First(x => x.Name == "SetException");
        return importer.ImportMethod(methodToFind);
    }

    public IMethodDefOrRef MoveNextValidationSetResultMethod(
        ReferenceImporter importer,
        ModuleDefinition gameModule
    )
    {
        var typeToFind = gameModule
            .GetAllTypes()
            .FirstOrDefault(dumpyReflectionHelper.GetRunValidationType);
        var methodToFind = typeToFind
            .NestedTypes[0]
            .Fields[1]
            .Signature.FieldType.Resolve()
            .Methods.First(x => x.Name == "SetResult");
        return importer.ImportMethod(methodToFind);
    }

    public MethodDefinition EnsureConsistencySucceedMethod(ModuleDefinition module)
    {
        var typeToFind = module
            .GetAllTypes()
            .FirstOrDefault(dumpyReflectionHelper.GetEnsureConsistencyType);
        var methodToFind = typeToFind
            .NestedTypes[0]
            .Methods.FirstOrDefault(x => x.Name == "Succeed");
        return methodToFind;
    }

    public IMethodDefOrRef DumpyStartDumpyMethod(
        ReferenceImporter importer,
        ModuleDefinition dumpModule
    )
    {
        var typeToFind = dumpModule.GetAllTypes().FirstOrDefault(x => x.Name == "DumpyTool");
        var methodToFind = typeToFind.Methods.FirstOrDefault(x => x.Name == "StartDumpyTask");
        return importer.ImportMethod(methodToFind);
    }
}
