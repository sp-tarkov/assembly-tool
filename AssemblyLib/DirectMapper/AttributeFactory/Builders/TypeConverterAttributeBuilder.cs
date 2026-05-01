using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AssemblyLib.Extensions;
using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.AttributeFactory.Builders;

[Injectable]
public class TypeConverterAttributeBuilder(ILogger<TypeConverterAttributeBuilder> logger, DataProvider dataProvider)
    : IAttributeBuilder
{
    public bool Enabled => true;

    public void Build()
    {
        var renameMap = new Dictionary<string, TypeDefinition>();
        foreach (var (fullName, mapping) in dataProvider.DirectMapModels)
        {
            renameMap.Add(fullName, mapping.ToolData.Type!);
        }

        var module = dataProvider.LoadedModule!;
        foreach (var type in module.GetAllTypes())
        {
            // Update attributes on the type itself
            UpdateTypeConvertersInTarget(type, renameMap);

            // Update attributes on properties
            foreach (var property in type.Properties)
            {
                UpdateTypeConvertersInTarget(property, renameMap);
            }

            // Update attributes on fields
            foreach (var field in type.Fields)
            {
                UpdateTypeConvertersInTarget(field, renameMap);
            }
        }
    }

    private void UpdateTypeConvertersInTarget(IHasCustomAttribute target, Dictionary<string, TypeDefinition> renameMap)
    {
        List<(CustomAttribute oldAttr, CustomAttribute newAttr)> replacements = [];

        foreach (var attr in target.CustomAttributes)
        {
            if (!attr.IsTypeConverterAttribute())
            {
                continue;
            }

            var referencedTypeName = attr.ExtractTypeNameFromAttribute();
            if (referencedTypeName != null && renameMap.TryGetValue(referencedTypeName, out var newTypeDef))
            {
                replacements.Add((attr, CreateTypeConverterAttribute(newTypeDef)));
            }
        }

        foreach (var (oldAttr, newAttr) in replacements)
        {
            target.CustomAttributes.Remove(oldAttr);
            target.CustomAttributes.Add(newAttr);
        }
    }

    private CustomAttribute CreateTypeConverterAttribute(TypeDefinition converterType)
    {
        var module = dataProvider.LoadedModule;
        var factory = module!.CorLibTypeFactory;

        // Get System.Type reference
        var sysTypeRef = factory
            .CorLibScope.CreateTypeReference("System", "Type")
            .ImportWith(module.DefaultImporter)
            .ToTypeSignature(false);

        // TypeConverterAttribute is in System.ComponentModel which is part of System
        // Find the System assembly reference (or System.ComponentModel if it's separate)
        var systemAssembly = module.AssemblyReferences.FirstOrDefault(a =>
            a.Name == "System" || a.Name == "System.ComponentModel" || a.Name == "System.ComponentModel.TypeConverter"
        );

        if (systemAssembly == null)
        {
            // Fallback to creating System reference
            systemAssembly = new AssemblyReference("System", new Version(4, 0, 0, 0));
            module.AssemblyReferences.Add(systemAssembly);
        }

        // Create TypeConverterAttribute constructor reference from the correct assembly
        var typeConverterAttrRef = new TypeReference(
            module,
            systemAssembly,
            "System.ComponentModel",
            "TypeConverterAttribute"
        )
            .CreateMemberReference(".ctor", MethodSignature.CreateInstance(factory.Void, [sysTypeRef]))
            .ImportWith(module.DefaultImporter);

        var customAttribute = new CustomAttribute(typeConverterAttrRef)
        {
            Signature = new CustomAttributeSignature(
                new CustomAttributeArgument(sysTypeRef, converterType.ToTypeSignature())
            ),
        };

        return customAttribute;
    }
}
