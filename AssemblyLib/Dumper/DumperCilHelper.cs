using AsmResolver.DotNet;

namespace AssemblyLib.Dumper;

public static class DumperCilHelper
{
    public static IMethodDefOrRef RunValidationCreateMethod(ReferenceImporter importer, ModuleDefinition gameModule)
    {
        var typeToFind = gameModule.GetAllTypes().FirstOrDefault(DumpyReflectionHelper.GetRunValidationType);
        var methodToFind = typeToFind.NestedTypes[0].Fields[1].Signature.FieldType.Resolve().Methods.FirstOrDefault(x => x.Name == "Create");
        return importer.ImportMethod(methodToFind);
    }

    public static IMethodDefOrRef RunValidationGetTaskMethod(ReferenceImporter importer, ModuleDefinition gameModule)
    {
        var typeToFind = gameModule.GetAllTypes().FirstOrDefault(DumpyReflectionHelper.GetRunValidationType);
        var methodToFind = typeToFind.NestedTypes[0].Fields[1].Signature.FieldType.Resolve().Methods.FirstOrDefault(x => x.Name == "get_Task");
        return importer.ImportMethod(methodToFind);
    }

    public static FieldDefinition RunValidationFieldZero(ModuleDefinition gameModule)
    {
        var typeToFind = gameModule.GetAllTypes().FirstOrDefault(DumpyReflectionHelper.GetRunValidationType);
        var fieldToFind = typeToFind.NestedTypes[0].Fields[0];
        return fieldToFind;
    }

    public static FieldDefinition RunValidationFieldOne(ModuleDefinition gameModule)
    {
        var typeToFind = gameModule.GetAllTypes().FirstOrDefault(DumpyReflectionHelper.GetRunValidationType);
        var fieldToFind = typeToFind.NestedTypes[0].Fields[1];
        return fieldToFind;
    }

    public static FieldDefinition RunValidationFieldTwo(ModuleDefinition gameModule)
    {
        var typeToFind = gameModule.GetAllTypes().FirstOrDefault(DumpyReflectionHelper.GetRunValidationType);
        var fieldToFind = typeToFind.NestedTypes[0].Fields[2];
        return fieldToFind;
    }

    public static IMethodDescriptor BackRequestLogRequestResponseMethod(ReferenceImporter importer)
    {
        var methodToFind = typeof(DumpLib.DumpyTool).GetMethod("LogRequestResponse", new[] { typeof(object), typeof(object) });
        return importer.ImportMethod(methodToFind);
    }

    public static MethodDefinition MoveNextValidationSetSucceedMethod(ModuleDefinition gameModule)
    {
        var typeToFind = gameModule.GetAllTypes().FirstOrDefault(DumpyReflectionHelper.GetRunValidationType);
        var methodToFind = typeToFind.Methods.FirstOrDefault(x => x.Name == "set_Succeed");
        return methodToFind;
    }

    public static FieldDefinition MoveNextValidationFieldZero(ModuleDefinition gameModule)
    {
        var typeToFind = gameModule.GetAllTypes().FirstOrDefault(DumpyReflectionHelper.GetRunValidationType);
        var fieldToFind = typeToFind.NestedTypes[0].Fields[0];
        return fieldToFind;
    }

    public static FieldDefinition MoveNextValidationFieldOne(ModuleDefinition gameModule)
    {
        var typeToFind = gameModule.GetAllTypes().FirstOrDefault(DumpyReflectionHelper.GetRunValidationType);
        var fieldToFind = typeToFind.NestedTypes[0].Fields[1];
        return fieldToFind;
    }

    public static FieldDefinition MoveNextValidationFieldTwo(ModuleDefinition gameModule)
    {
        var typeToFind = gameModule.GetAllTypes().FirstOrDefault(DumpyReflectionHelper.GetRunValidationType);
        var fieldToFind = typeToFind.NestedTypes[0].Fields[2];
        return fieldToFind;
    }

    public static IMethodDefOrRef MoveNextValidationSetExceptionMethod(ReferenceImporter importer, ModuleDefinition gameModule)
    {
        var typeToFind = gameModule.GetAllTypes().FirstOrDefault(DumpyReflectionHelper.GetRunValidationType);
        var methodToFind = typeToFind.NestedTypes[0].Fields[1].Signature.FieldType.Resolve().Methods.First(x => x.Name == "SetException");
        return importer.ImportMethod(methodToFind);
    }

    public static IMethodDefOrRef MoveNextValidationSetResultMethod(ReferenceImporter importer, ModuleDefinition gameModule)
    {
        var typeToFind = gameModule.GetAllTypes().FirstOrDefault(DumpyReflectionHelper.GetRunValidationType);
        var methodToFind = typeToFind.NestedTypes[0].Fields[1].Signature.FieldType.Resolve().Methods.First(x => x.Name == "SetResult");
        return importer.ImportMethod(methodToFind);
    }

    public static MethodDefinition EnsureConsistencySucceedMethod(ModuleDefinition module)
    {
        var typeToFind = module.GetAllTypes().FirstOrDefault(DumpyReflectionHelper.GetEnsureConsistencyType);
        var methodToFind = typeToFind.NestedTypes[0].Methods.FirstOrDefault(x => x.Name == "Succeed");
        return methodToFind;
    }

    public static IMethodDefOrRef DumpyStartDumpyMethod(ReferenceImporter importer, ModuleDefinition dumpModule)
    {
        var typeToFind = dumpModule.GetAllTypes().FirstOrDefault(x => x.Name == "DumpyTool");
        var methodToFind = typeToFind.Methods.FirstOrDefault(x => x.Name == "StartDumpyTask");
        return importer.ImportMethod(methodToFind);
    }
}