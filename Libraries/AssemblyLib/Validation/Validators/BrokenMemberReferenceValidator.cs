using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AssemblyLib.Helpers;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Validation.Validators;

/// <summary>
///     Post-mapping validator. Finds MemberReferences that no longer resolve after renaming —
///     a sign that a reference was not updated when its target method/field was renamed.
///     Only same-assembly references are checked; cross-assembly refs are out of scope.
/// </summary>
[Injectable]
public class BrokenMemberReferenceValidator(
    ILogger<BrokenMemberReferenceValidator> logger,
    DataProvider dataProvider,
    MemberReferenceCache memberReferenceCache
) : IAssemblyValidator
{
    public string Name => "Broken Member References";
    public bool Enabled => true;
    public int Priority => 5;
    public ValidationStage Stage => ValidationStage.PostMapping;

    public IReadOnlyList<ValidationIssue> Validate()
    {
        var issues = new List<ValidationIssue>();

        if (dataProvider.LoadedModule is null)
        {
            return issues;
        }

        foreach (var reference in dataProvider.LoadedModule.GetImportedMemberReferences())
        {
            if (!IsInternalReference(reference))
            {
                continue;
            }

            if (reference.TryResolve(dataProvider.Context, out _))
            {
                continue;
            }

            // References through generic instantiations always fail TryResolve — try the open type
            if (TryResolveViaGenericType(reference))
            {
                continue;
            }

            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Error,
                    Name,
                    $"Unresolvable reference to '{reference.Name}' on '{reference.DeclaringType?.FullName ?? "unknown"}'"
                )
            );
        }

        return issues;
    }

    /// <summary>
    ///     Returns true if the reference targets a type in the assembly being remapped.
    /// </summary>
    private bool IsInternalReference(MemberReference reference)
    {
        var declType = reference.DeclaringType;

        // Unwrap TypeSpec (generic instantiation) to the element type
        if (declType is TypeSpecification { Signature: GenericInstanceTypeSignature genericSig })
        {
            declType = genericSig.GenericType;
        }

        return declType switch
        {
            // TypeDef tokens always belong to the current assembly by definition
            TypeDefinition => true,
            // TypeRef with no AssemblyReference scope resolves within this module
            TypeReference { Scope: not AssemblyReference } => true,
            _ => false,
        };
    }

    private bool TryResolveViaGenericType(MemberReference reference)
    {
        if (reference.DeclaringType is not TypeSpecification { Signature: GenericInstanceTypeSignature genericSig })
        {
            return false;
        }

        return genericSig.GenericType.TryResolve(dataProvider.Context, out _);
    }
}
