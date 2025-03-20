/*
using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using AssemblyLib.Application;
using AssemblyLib.Models;
using AssemblyLib.Utils;

namespace AssemblyLib.ReMapper.MetaData;

public class AttributeFactory(ModuleDefinition module, List<TypeDefinition> types)
    : IComponent
{
    private MethodDefinition? _sptRenamedAttrCtorDef;
    
    public async Task CreateCustomTypeAttribute()
    {
        var corlibRef = new AssemblyRefUser(module.GetCorlibAssembly());

        // Create the attribute
        var annotationType = new TypeDefinition(
            "SPT",
            "SPTRenamedClassAttribute",
            TypeAttributes.Public | TypeAttributes.AutoLayout | TypeAttributes.Class | TypeAttributes.AnsiClass)
        {
            BaseType = new TypeRefUser(module, "System", "Attribute", corlibRef),
        };

        // Add fields
        annotationType.Fields.Add(new FieldDefUser(
            "RenamedFrom",
            new FieldSig(module.CorLibTypes.String),
            FieldAttributes.Public | FieldAttributes.InitOnly));

        annotationType.Fields.Add(new FieldDefUser(
            "HasChangesFromPreviousVersion",
            new FieldSig(module.CorLibTypes.Boolean),
            FieldAttributes.Public | FieldAttributes.InitOnly));

        // Create the constructor
        var ctor = new MethodDefUser(".ctor",
            MethodSig.CreateInstance(module.CorLibTypes.Void, module.CorLibTypes.String, module.CorLibTypes.Boolean),
            MethodImplAttributes.IL | MethodImplAttributes.Managed,
            MethodAttributes.Public |
            MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);

        // Add the ctor method
        annotationType.Methods.Add(ctor);

        // Name the ctor parameters
        ctor.Parameters[1].CreateParamDef();
        ctor.Parameters[1].ParamDef.Name = "renamedFrom";
        ctor.Parameters[2].CreateParamDef();
        ctor.Parameters[2].ParamDef.Name = "hasChangesFromPreviousVersion";

        // Create the body
        ctor.Body = new CilBody();

        ctor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
        ctor.Body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
        ctor.Body.Instructions.Add(OpCodes.Stfld.ToInstruction(annotationType.Fields[0]));

        ctor.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
        ctor.Body.Instructions.Add(OpCodes.Ldarg_2.ToInstruction());
        ctor.Body.Instructions.Add(OpCodes.Stfld.ToInstruction(annotationType.Fields[1]));
        ctor.Body.Instructions.Add(OpCodes.Ret.ToInstruction());

        // Add the attribute to the assembly
        module.Types.Add(annotationType);

        _sptRenamedAttrCtorDef = annotationType.FindMethod(".ctor");
        
        await AddMetaDataAttributeToTypes();
    }

    private async Task AddMetaDataAttributeToTypes()
    {
        var attrTasks = new List<Task>(DataProvider.Remaps.Count);
        var diff = Context.Instance.Get<DiffCompare>();

        foreach (var mapping in DataProvider.Remaps)
        {
            attrTasks.Add(
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        AddMetaDataAttributeToTypes(mapping, diff);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Exception in task: {ex.Message}", ConsoleColor.Red);
                    }
                })
            );
        }

        if (DataProvider.Settings.DebugLogging)
        {
            await Task.WhenAll(attrTasks);
            return;
        }
        
        await Logger.DrawProgressBar(attrTasks, "Applying Custom Attribute");
    }
    
    private void AddMetaDataAttributeToTypes(RemapModel remap, DiffCompare? diff)
    {
        var customAttribute = new CustomAttribute(module.Import(_sptRenamedAttrCtorDef));
        customAttribute.ConstructorArguments.Add(new CAArgument(module.CorLibTypes.String, remap.OriginalTypeName));

        if (diff is not null)
        {
            customAttribute.ConstructorArguments.Add(new CAArgument(module.CorLibTypes.Boolean,
                diff.IsSame(remap.TypePrimeCandidate!)));
        }

        remap.TypePrimeCandidate!.CustomAttributes.Add(customAttribute);
    }
    
    public void UpdateAsyncAttributes()
    {
        foreach (var type in DataProvider.Remaps.Select(r => r.TypePrimeCandidate))
        {
            if (!type.HasNestedTypes) continue;
            
            foreach (var method in type.Methods)
            {
                if (method.HasCustomAttributes)
                {
                    UpdateAttributeCollection(method.CustomAttributes, type.NestedTypes);
                }
            }
        }
    }
    
    private void UpdateAttributeCollection(CustomAttributeCollection customAttributes, IList<TypeDef> nestedTypes)
    {
        // Key - Old :: Val - New
        Dictionary<CustomAttribute, CustomAttribute> attrReplacements = [];
        
        foreach (var attr in customAttributes.ToArray())
        {
            if (attr.AttributeType.FullName != "System.Runtime.CompilerServices.AsyncStateMachineAttribute") continue;
            
            var attrConstructorArgument = attr.ConstructorArguments[0];
            
            var originalRef = attrConstructorArgument.Value as ClassSig ?? throw new InvalidCastException("Could not cast constructor argument to ClassSig");
            
            // Find the argument target in the nested types
            var typeDefTarget = nestedTypes.FirstOrDefault(t => t.Name == originalRef.TypeName);
            
            //Logger.Log($"Updating {originalRef.FullName} to {typeDefTarget!.FullName}");
            
            attrReplacements.Add(attr, CreateNewAsyncAttribute(typeDefTarget!));
        }
        
        foreach (var replacement in attrReplacements)
        {
            customAttributes.Remove(replacement.Key);
            customAttributes.Add(replacement.Value);
        }
    }
    
    private CustomAttribute CreateNewAsyncAttribute(TypeDef targetTypeDef)
    {
        var corlibRef = new AssemblyRefUser(module.GetCorlibAssembly());

        var asyncStateMachineAttributeTypeRef = new TypeRefUser(
            module, 
            "System.Runtime.CompilerServices", 
            "AsyncStateMachineAttribute", 
            corlibRef);
        
        var asyncStateMachineAttributeTypeDef = asyncStateMachineAttributeTypeRef.ResolveTypeDefThrow();
        
        var systemTypeRef = new TypeRefUser(
            module, 
            "System", 
            "Type", 
            corlibRef);
        
        var systemTypeSig = systemTypeRef.ToTypeSig();
        
        var constructor = asyncStateMachineAttributeTypeDef.FindMethod(
            ".ctor", MethodSig.CreateInstance(module.CorLibTypes.Void,systemTypeSig));
        
        if (constructor is null)
        {
            throw new Exception("AsyncStateMachineAttribute constructor not found.");
        }
        
        // Create a custom attribute.
        var customAttribute = new CustomAttribute(
            new MemberRefUser(
                module, 
                ".ctor", 
                MethodSig.CreateInstance(module.CorLibTypes.Void, systemTypeSig), 
                asyncStateMachineAttributeTypeRef));

        customAttribute.ConstructorArguments.Add(new CAArgument(systemTypeSig, targetTypeDef.ToTypeSig()));
        
        return customAttribute;
    }
}
*/