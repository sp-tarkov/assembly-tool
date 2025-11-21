using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.DotNet.Signatures.Types;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using AssemblyLib.Models;
using AssemblyLib.Shared;
using Serilog;
using Serilog.Events;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper;

[Injectable]
public class AttributeFactory(DataProvider dataProvider)
{
    public void UpdateAsyncAttributes(ModuleDefinition module)
    {
        /*
        foreach (var type in dataProvider.GetRemaps().Select(r => r.ChosenType))
        {
            if (type is null)
            {
                Log.Error("Type was null, skipping ...");

                continue;
            }

            if (type.NestedTypes.Count == 0)
            {
                continue;
            }

            foreach (var method in type.Methods)
            {
                if (IsAsyncMethod(method))
                {
                    UpdateAttributeCollection(module, method, type.NestedTypes);
                }
            }
        }
        */
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
            if (!IsAsyncStateMachineAttribute(attr))
            {
                continue;
            }

            // Find the argument target in the nested types
            var typeDefTarget = nestedTypes.FirstOrDefault(t =>
                t.Name == ((TypeDefOrRefSignature)attr.Signature?.FixedArguments[0].Element!).Name
            );

            if (typeDefTarget is null)
            {
                Log.Error(
                    "Failed to locate AsyncStateMachineAttribute for method {DeclaringTypeName}::{MethodName}",
                    method.DeclaringType?.Name?.ToString(),
                    method.Name?.ToString()
                );
                continue;
            }

            attrReplacements.Add(attr, CreateNewAsyncAttribute(module, typeDefTarget));
        }

        foreach (var replacement in attrReplacements)
        {
            if (Log.IsEnabled(LogEventLevel.Error))
            {
                Log.Debug(
                    "Updating AsyncStateMachineAttribute for method {DeclaringTypeName}::{MethodName}",
                    method.DeclaringType?.Name?.ToString(),
                    method.Name?.ToString()
                );
            }

            method.CustomAttributes.Remove(replacement.Key);
            method.CustomAttributes.Add(replacement.Value);
        }
    }

    private CustomAttribute CreateNewAsyncAttribute(ModuleDefinition module, TypeDefinition targetTypeDef)
    {
        var factory = module.CorLibTypeFactory;

        var sysTypeRef = factory
            .CorLibScope.CreateTypeReference("System", "Type")
            .ImportWith(module.DefaultImporter)
            .ToTypeSignature();

        var asyncAttrRef = factory
            .CorLibScope.CreateTypeReference("System.Runtime.CompilerServices", "AsyncStateMachineAttribute")
            .CreateMemberReference(".ctor", MethodSignature.CreateInstance(module.CorLibTypeFactory.Void, sysTypeRef))
            .ImportWith(module.DefaultImporter);

        // Create a custom attribute.
        var customAttribute = new CustomAttribute(asyncAttrRef);

        var targetSig = targetTypeDef.ToTypeSignature();

        customAttribute.Signature?.FixedArguments.Add(new CustomAttributeArgument(sysTypeRef, targetSig));

        return customAttribute;
    }

    private static bool IsAsyncMethod(MethodDefinition method)
    {
        return method
            .CustomAttributes.Select(s => s.Constructor?.DeclaringType?.FullName)
            .Contains("System.Runtime.CompilerServices.AsyncStateMachineAttribute");
    }

    private static bool IsAsyncStateMachineAttribute(CustomAttribute attr)
    {
        return attr.Constructor?.DeclaringType?.FullName
            == "System.Runtime.CompilerServices.AsyncStateMachineAttribute";
    }
}
