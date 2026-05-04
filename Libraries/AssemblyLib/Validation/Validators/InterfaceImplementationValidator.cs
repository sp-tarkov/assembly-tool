using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Validation.Validators;

/// <summary>
///     Post-mapping validator. For every type that lists an <see cref="InterfaceImplementation"/>:
///     <list type="bullet">
///       <item>The interface reference itself must resolve.</item>
///       <item>Every non-static abstract method on the interface must have a matching method
///             on the implementing type — either by signature, or via an explicit
///             <see cref="MethodImplementation"/> override.</item>
///     </list>
///
///     Renames that touch interface-implementing types frequently leave one of the two sides
///     stale; this catches the usual <c>TypeLoadException: ... does not implement ...</c> case
///     before runtime.
/// </summary>
[Injectable]
public class InterfaceImplementationValidator(
    ILogger<InterfaceImplementationValidator> logger,
    DataProvider dataProvider
) : IAssemblyValidator
{
    public string Name => "Interface Implementations";
    public bool Enabled => true;
    public int Priority => 4;
    public ValidationStage Stage => ValidationStage.PostMapping;

    public IReadOnlyList<ValidationIssue> Validate()
    {
        var issues = new List<ValidationIssue>();

        if (dataProvider.LoadedModule is null)
        {
            return issues;
        }

        foreach (var type in dataProvider.LoadedModule.GetAllTypes())
        {
            // Interfaces themselves don't have to implement their own members.
            if (type.IsInterface || type.IsAbstract)
            {
                continue;
            }

            foreach (var impl in type.Interfaces)
            {
                CheckImplementation(type, impl, issues);
            }
        }

        return issues;
    }

    private void CheckImplementation(
        TypeDefinition implementer,
        InterfaceImplementation impl,
        List<ValidationIssue> issues
    )
    {
        if (impl.Interface is null)
        {
            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Error,
                    Name,
                    $"Type '{implementer.FullName}' has an InterfaceImplementation row with a null Interface"
                )
            );
            return;
        }

        if (!impl.Interface.TryResolve(dataProvider.Context, out var ifaceDef))
        {
            // Cross-assembly interface that we can't see — best effort, downgrade to warning.
            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Warning,
                    Name,
                    $"Type '{implementer.FullName}' implements '{impl.Interface.FullName}' "
                        + "which could not be resolved in the runtime context"
                )
            );
            return;
        }

        // For a generic interface like INestable<RadioTransmitterPacket>, the impl row points at
        // a TypeSpec whose Signature is a GenericInstanceTypeSignature. That signature is itself
        // an IGenericArgumentsProvider — feed it into a GenericContext so we can substitute the
        // interface method's open `T` for the concrete type before comparing signatures.
        var genericContext = default(GenericContext);
        if (impl.Interface is TypeSpecification { Signature: GenericInstanceTypeSignature genSig })
        {
            genericContext = new GenericContext(genSig, null);
        }

        var hierarchy = CollectHierarchy(implementer);

        foreach (var ifaceMethod in ifaceDef.Methods)
        {
            if (!ifaceMethod.IsAbstract || ifaceMethod.IsStatic)
            {
                continue;
            }

            var expectedSig = ifaceMethod.Signature;
            if (expectedSig is not null && !genericContext.IsEmpty)
            {
                expectedSig = expectedSig.InstantiateGenericTypes(genericContext);
            }

            if (HasMatchingImplementation(ifaceMethod, expectedSig, hierarchy))
            {
                continue;
            }

            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Error,
                    Name,
                    $"Type '{implementer.FullName}' is missing implementation of "
                        + $"'{ifaceDef.FullName}::{ifaceMethod.Name}'"
                )
            );
        }
    }

    /// <summary>
    ///     Walks the base-type chain, pairing each ancestor with the <see cref="GenericContext"/>
    ///     that should be applied to its members. For <c>EftClientBackend → ClientBackend&lt;IEftSession&gt;</c>
    ///     the entry for <c>ClientBackend</c> carries a context that maps its own <c>T</c> to
    ///     <c>IEftSession</c>, so its <c>get_Session</c> return type substitutes correctly when
    ///     compared against the (already-substituted) interface signature.
    ///     Contexts are composed across the chain: if a base appears as a generic instance whose
    ///     type arguments reference the current type's generic parameters, those references are
    ///     resolved through the current context before becoming the next context.
    /// </summary>
    private List<(TypeDefinition Type, GenericContext Context)> CollectHierarchy(TypeDefinition type)
    {
        var result = new List<(TypeDefinition, GenericContext)>();
        var current = type;
        var currentContext = default(GenericContext);
        var guard = 0;

        while (current is not null && guard++ < 64)
        {
            result.Add((current, currentContext));

            if (current.BaseType is null)
            {
                break;
            }

            // Build the context for the next ancestor *before* resolving it: the generic
            // arguments live on this type's BaseType reference, in the scope of `current`.
            var nextContext = default(GenericContext);
            if (current.BaseType is TypeSpecification { Signature: GenericInstanceTypeSignature gis })
            {
                // If the current type itself sits under an active context, the arguments here
                // may reference *its* generic parameters — instantiate them through before use.
                var provider = currentContext.IsEmpty
                    ? gis
                    : gis.InstantiateGenericTypes(currentContext) as GenericInstanceTypeSignature ?? gis;

                nextContext = new GenericContext(provider, null);
            }

            if (!current.BaseType.TryResolve(dataProvider.Context, out var baseDef))
            {
                break;
            }

            current = baseDef;
            currentContext = nextContext;
        }

        return result;
    }

    private static bool HasMatchingImplementation(
        MethodDefinition ifaceMethod,
        MethodSignature? expectedSig,
        List<(TypeDefinition Type, GenericContext Context)> hierarchy
    )
    {
        // Explicit interface implementations (.override rows) — walk every ancestor, since
        // the row lives on whichever type physically provides the body.
        var ifaceTypeName = ifaceMethod.DeclaringType?.FullName;

        foreach (var (ancestor, _) in hierarchy)
        {
            foreach (var methodImpl in ancestor.MethodImplementations)
            {
                if (methodImpl.Declaration is not { } decl)
                {
                    continue;
                }

                if (decl.Name != ifaceMethod.Name)
                {
                    continue;
                }

                if (UnwrapToOpenType(decl.DeclaringType)?.FullName == ifaceTypeName)
                {
                    return true;
                }
            }
        }

        if (expectedSig is null)
        {
            return hierarchy.SelectMany(h => h.Type.Methods).Any(m => m.Name == ifaceMethod.Name);
        }

        // Implicit impls (same name + matching signature). For each ancestor, instantiate the
        // candidate's signature through that ancestor's generic context first — otherwise a
        // method on ClientBackend<T> returning !0 would be compared as "!0" against the
        // interface's already-substituted return type "EFT.IEftSession" and miss.
        foreach (var (ancestor, context) in hierarchy)
        {
            foreach (var candidate in ancestor.Methods)
            {
                if (candidate.Name != ifaceMethod.Name)
                {
                    continue;
                }

                var candidateSig = candidate.Signature;
                if (candidateSig is not null && !context.IsEmpty)
                {
                    candidateSig = candidateSig.InstantiateGenericTypes(context);
                }

                if (SignaturesMatch(candidateSig, expectedSig))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static ITypeDefOrRef? UnwrapToOpenType(ITypeDefOrRef? type)
    {
        // The .override row's declaring type for a generic interface impl is a TypeSpec
        // wrapping a GenericInstanceTypeSignature; the GenericType inside is the open
        // ref/def we want to compare against ifaceMethod.DeclaringType.
        if (type is TypeSpecification { Signature: GenericInstanceTypeSignature gis })
        {
            return gis.GenericType;
        }

        return type;
    }

    private static bool SignaturesMatch(MethodSignature? a, MethodSignature b)
    {
        if (a is null)
        {
            return false;
        }

        if (a.GenericParameterCount != b.GenericParameterCount)
        {
            return false;
        }

        if (a.ParameterTypes.Count != b.ParameterTypes.Count)
        {
            return false;
        }

        // Compare by full name string — covers our same-module renames without paying for a full
        // structural-equality walk over signatures (which would also need a generic context).
        if (a.ReturnType.FullName != b.ReturnType.FullName)
        {
            return false;
        }

        for (var i = 0; i < a.ParameterTypes.Count; i++)
        {
            if (a.ParameterTypes[i].FullName != b.ParameterTypes[i].FullName)
            {
                return false;
            }
        }

        return true;
    }
}
