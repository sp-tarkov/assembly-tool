
using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.DotNet.Signatures.Types;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using AssemblyLib.Models;
using AssemblyLib.Utils;
using Serilog;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.ReMapper.MetaData;

[Injectable]
public class AttributeFactory(DiffCompare diffCompare)
{
    private ICustomAttributeType? _sptRenamedDef;
    
    public async Task CreateCustomTypeAttribute(ModuleDefinition module)
    {
        var customAttribute = new TypeDefinition(
            "SPT",
            "SPTRenamedClassAttribute",
            TypeAttributes.Public | TypeAttributes.AutoLayout | TypeAttributes.Class | TypeAttributes.AnsiClass,
            DataProvider.Mscorlib.GetAllTypes().First(t => t.FullName == "System.Attribute")
                .ImportWith(module.DefaultImporter)
        );
        
        // Add fields
        customAttribute.Fields.Add(new FieldDefinition(
            new Utf8String("RenamedFrom"),
            FieldAttributes.Public | FieldAttributes.InitOnly,
            new FieldSignature(module.CorLibTypeFactory.String)));

        customAttribute.Fields.Add(new FieldDefinition(
            new Utf8String("HasChangesFromPreviousVersion"),
            FieldAttributes.Public | FieldAttributes.InitOnly,
            new FieldSignature(module.CorLibTypeFactory.Boolean)));

        // Create the constructor
        var ctor = new MethodDefinition(
            new Utf8String(".ctor"),
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RuntimeSpecialName,
            new MethodSignature(
                CallingConventionAttributes.Default, 
                module.CorLibTypeFactory.Void, 
                [ module.CorLibTypeFactory.String, module.CorLibTypeFactory.Boolean ])
            );

        // Add the ctor method
        customAttribute.Methods.Add(ctor);
        
        // Name the ctor parameters
        ctor.Parameters[0].GetOrCreateDefinition();
        ctor.Parameters[0].Definition!.Name = new Utf8String("renamedFrom");
        ctor.Parameters[1].GetOrCreateDefinition();
        ctor.Parameters[1].Definition!.Name = new Utf8String("hasChangesFromPreviousVersion");

        ctor.CilMethodBody = new CilMethodBody(ctor);

        ctor.CilMethodBody.Instructions.Add(new CilInstruction(CilOpCodes.Ldarg_0));
        ctor.CilMethodBody.Instructions.Add(new CilInstruction(CilOpCodes.Ldarg, (ushort)0));
        ctor.CilMethodBody.Instructions.Add(new CilInstruction(CilOpCodes.Stfld, customAttribute.Fields[0]));
        ctor.CilMethodBody.Instructions.Add(new CilInstruction(CilOpCodes.Ldarg_0));
        ctor.CilMethodBody.Instructions.Add(new CilInstruction(CilOpCodes.Ldarg, (ushort)1));
        ctor.CilMethodBody.Instructions.Add(new CilInstruction(CilOpCodes.Stfld, customAttribute.Fields[1]));
        ctor.CilMethodBody.Instructions.Add(new CilInstruction(CilOpCodes.Ret));
        
        // Add the attribute to the assembly
        module.TopLevelTypes.Add(customAttribute);
        
        await AddMetaDataAttributeToTypes(module);
    }

    private async Task AddMetaDataAttributeToTypes(ModuleDefinition module)
    {
        var attrTasks = new List<Task>(DataProvider.Remaps.Count);
        
        foreach (var mapping in DataProvider.Remaps)
        {
            attrTasks.Add(
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        AddMetaDataAttributeToTypes(module, mapping);
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Exception in task: {ExMessage}", ex.Message);
                    }
                })
            );
        }
        
        await Task.WhenAll(attrTasks);
    }
    
    private void AddMetaDataAttributeToTypes(
        ModuleDefinition module, 
        RemapModel remap
        )
    {
        var typeRef = new TypeReference(module, "SPT", "SPTRenamedClassAttribute")
            .ImportWith(module.DefaultImporter);
        
        var ctor = typeRef.Resolve()?.GetConstructor(SignatureComparer.Default, [ module.CorLibTypeFactory.String, module.CorLibTypeFactory.Boolean ]);
        
        var customAttribute = new CustomAttribute(ctor);
        
        customAttribute.Signature?.FixedArguments.Add(
            new CustomAttributeArgument(
                module.CorLibTypeFactory.String, 
                remap.OriginalTypeName)
            );

        remap.TypePrimeCandidate!.CustomAttributes.Add(customAttribute);
    }
    
    public void UpdateAsyncAttributes(ModuleDefinition module)
    {
        Log.Information("Updating Async Attributes...");
        
        foreach (var type in DataProvider.Remaps.Select(r => r.TypePrimeCandidate))
        {
            if (type is null)
            {
                Log.Error("Type was null, skipping ...");

                continue;
            }

            if (type.NestedTypes.Count == 0) continue;
            
            foreach (var method in type.Methods)
            {
                if (IsAsyncMethod(method))
                {
                    UpdateAttributeCollection(module, method, type.NestedTypes);
                }
            }
        }
    }
    
    private void UpdateAttributeCollection(
        ModuleDefinition module,
        MethodDefinition method, 
        IList<TypeDefinition> nestedTypes
        )
    {
        // Key - Old :: Val - New
        Dictionary<CustomAttribute, CustomAttribute> attrReplacements = [];
        
        foreach (var attr in method.CustomAttributes.ToArray())
        {
            if (!IsAsyncStateMachineAttribute(attr)) continue;

            // Find the argument target in the nested types
            var typeDefTarget = nestedTypes
                .FirstOrDefault(t => t.Name == ((TypeDefOrRefSignature)attr.Signature?.FixedArguments[0].Element).Name);

            if (typeDefTarget is null)
            {
                Log.Error("Failed to locate AsyncStateMachineAttribute for method {DeclaringTypeName}::{MethodName}", 
                    method.DeclaringType?.Name?.ToString(), 
                    method.Name?.ToString());
                continue;
            }
            
            attrReplacements.Add(attr, CreateNewAsyncAttribute(module, typeDefTarget!));
        }
        
        foreach (var replacement in attrReplacements)
        {
            Log.Information("Updating AsyncStateMachineAttribute for method {DeclaringTypeName}::{MethodName}", 
                method.DeclaringType?.Name?.ToString(), 
                method.Name?.ToString()
                );
            
            method.CustomAttributes.Remove(replacement.Key);
            method.CustomAttributes.Add(replacement.Value);
        }
    }
    
    private CustomAttribute CreateNewAsyncAttribute(
        ModuleDefinition module, 
        TypeDefinition targetTypeDef
        )
    {
        var factory = module.CorLibTypeFactory;

        var sysTypeRef = factory.CorLibScope
            .CreateTypeReference("System", "Type")
            .ImportWith(module.DefaultImporter)
            .ToTypeSignature();
            
        var asyncAttrRef = factory.CorLibScope
            .CreateTypeReference("System.Runtime.CompilerServices", "AsyncStateMachineAttribute")
            .CreateMemberReference(".ctor", MethodSignature.CreateInstance(module.CorLibTypeFactory.Void, sysTypeRef))
            .ImportWith(module.DefaultImporter);
            
        // Create a custom attribute.
        var customAttribute = new CustomAttribute(asyncAttrRef);
        
        var targetSig = targetTypeDef.ToTypeSignature();
            
        customAttribute.Signature?.FixedArguments.Add(
            new CustomAttributeArgument(sysTypeRef, targetSig)
        );
        
        return customAttribute;
    }

    private static bool IsAsyncMethod(MethodDefinition method)
    {
        return method.CustomAttributes.Select(s => s.Constructor?.DeclaringType?.FullName)
            .Contains("System.Runtime.CompilerServices.AsyncStateMachineAttribute");
    }

    private static bool IsAsyncStateMachineAttribute(CustomAttribute attr)
    {
        return attr.Constructor?.DeclaringType?.FullName ==
               "System.Runtime.CompilerServices.AsyncStateMachineAttribute";
    }
}