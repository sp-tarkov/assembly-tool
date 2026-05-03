using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.AttributeFactory.Builders;

[Injectable]
public class AsyncAttributeBuilder(ILogger<AsyncAttributeBuilder> logger, DataProvider dataProvider) : IAttributeBuilder
{
    public bool Enabled => true;

    public void Build()
    {
        var types = dataProvider.LoadedModule!.GetAllTypes();

        foreach (var type in types)
        {
            if (type.NestedTypes.Count == 0)
            {
                continue;
            }

            foreach (var method in type.Methods)
            {
                if (method.IsAsyncMethod())
                {
                    UpdateAsyncAttribute(method, type.NestedTypes);
                }
            }
        }
    }

    private void UpdateAsyncAttribute(MethodDefinition method, IList<TypeDefinition> nestedTypes)
    {
        // Key - Old :: Val - New
        Dictionary<CustomAttribute, CustomAttribute> attrReplacements = [];

        foreach (var attr in method.CustomAttributes.ToArray())
        {
            if (!attr.IsAsyncStateMachineAttribute())
            {
                continue;
            }

            // Find the argument target in the nested types
            var typeDefTarget = nestedTypes.FirstOrDefault(t =>
                t.Name == ((TypeDefOrRefSignature)attr.Signature?.FixedArguments[0].Element!).Name
            );

            if (typeDefTarget is null)
            {
                logger.LogError(
                    "Failed to locate AsyncStateMachineAttribute for method {DeclaringTypeName}::{MethodName}",
                    method.DeclaringType?.Name?.ToString(),
                    method.Name?.ToString()
                );
                continue;
            }

            attrReplacements.Add(attr, CreateNewAsyncAttribute(typeDefTarget));
        }

        foreach (var replacement in attrReplacements)
        {
            method.CustomAttributes.Remove(replacement.Key);
            method.CustomAttributes.Add(replacement.Value);
        }
    }

    private CustomAttribute CreateNewAsyncAttribute(TypeDefinition targetTypeDef)
    {
        var module = dataProvider.LoadedModule;
        var factory = module!.CorLibTypeFactory;

        var sysTypeRef = factory
            .CorLibScope.CreateTypeReference("System", "Type")
            .ImportWith(module.DefaultImporter)
            .ToTypeSignature(false);

        var asyncAttrRef = factory
            .CorLibScope.CreateTypeReference("System.Runtime.CompilerServices", "AsyncStateMachineAttribute")
            .CreateMemberReference(".ctor", MethodSignature.CreateInstance(module.CorLibTypeFactory.Void, [sysTypeRef]))
            .ImportWith(module.DefaultImporter);

        // Create a custom attribute.
        var customAttribute = new CustomAttribute(asyncAttrRef);

        var targetSig = targetTypeDef.ToTypeSignature();

        customAttribute.Signature?.FixedArguments.Add(new CustomAttributeArgument(sysTypeRef, targetSig));

        return customAttribute;
    }
}
