using AssemblyLib.Application;
using AssemblyLib.Models;
using AssemblyLib.Utils;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using FieldAttributes = dnlib.DotNet.FieldAttributes;
using MethodAttributes = dnlib.DotNet.MethodAttributes;
using MethodImplAttributes = dnlib.DotNet.MethodImplAttributes;
using TypeAttributes = dnlib.DotNet.TypeAttributes;

namespace AssemblyLib.ReMapper.MetaData;

public class AttributeFactory(ModuleDefMD module, List<TypeDef> types)
    : IComponent
{
    private MethodDef? _sptRenamedAttrCtorDef;
    
    public async Task CreateCustomTypeAttribute()
    {
        var corlibRef = new AssemblyRefUser(module.GetCorlibAssembly());

        // Create the attribute
        var annotationType = new TypeDefUser(
            "SPT",
            "SPTRenamedClassAttribute",
            module.CorLibTypes.Object.TypeDefOrRef)
        {
            Attributes = TypeAttributes.Public | TypeAttributes.AutoLayout |
                         TypeAttributes.Class | TypeAttributes.AnsiClass,

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
        
        await AddAttributeToTypes();
    }

    private async Task AddAttributeToTypes()
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
                        AddAttributeToType(mapping, diff);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Exception in task: {ex.Message}", ConsoleColor.Red);
                    }
                })
            );
        }
        
        await Logger.DrawProgressBar(attrTasks, "Applying Custom Attribute");

        //Task.WaitAll(attrTasks.ToArray());
    }
    
    private void AddAttributeToType(RemapModel remap, DiffCompare? diff)
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
}