using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Validation.Validators;

/// <summary>
///     Post-mapping validator. Walks every <see cref="GenericInstanceTypeSignature"/> reachable
///     from the module and confirms its <c>TypeArguments</c> count matches the resolved
///     definition's generic-parameter count.
///
///     A mismatch produces a <c>TypeLoadException</c> at runtime ("the generic type ... was used
///     with the wrong number of generic arguments") and is a common fallout of renaming a
///     generic type without updating the matching signatures.
/// </summary>
[Injectable]
public class GenericInstanceValidator(
    ILogger<GenericInstanceValidator> logger,
    DataProvider dataProvider
) : IAssemblyValidator
{
    public string Name => "Generic Instances";
    public bool Enabled => true;
    public int Priority => 6;
    public ValidationStage Stage => ValidationStage.PostMapping;

    public IReadOnlyList<ValidationIssue> Validate()
    {
        var issues = new List<ValidationIssue>();

        if (dataProvider.LoadedModule is null)
        {
            return issues;
        }

        var module = dataProvider.LoadedModule;
        var visited = new HashSet<TypeSignature>(ReferenceEqualityComparer.Instance);

        void Walk(TypeSignature? sig, string context)
        {
            if (sig is null || !visited.Add(sig))
            {
                return;
            }

            switch (sig)
            {
                case GenericInstanceTypeSignature gen:
                    CheckGenericInstance(gen, context, issues);
                    foreach (var arg in gen.TypeArguments)
                    {
                        Walk(arg, context);
                    }
                    break;
                case CustomModifierTypeSignature mod:
                    Walk(mod.BaseType, context);
                    break;
                case TypeSpecificationSignature spec:
                    Walk(spec.BaseType, context);
                    break;
                case FunctionPointerTypeSignature fp when fp.Signature is { } fpSig:
                    Walk(fpSig.ReturnType, context);
                    foreach (var p in fpSig.ParameterTypes)
                    {
                        Walk(p, context);
                    }
                    break;
            }
        }

        foreach (var type in module.GetAllTypes())
        {
            foreach (var field in type.Fields)
            {
                Walk(field.Signature?.FieldType, $"field '{type.FullName}::{field.Name}'");
            }

            foreach (var prop in type.Properties)
            {
                if (prop.Signature is null)
                {
                    continue;
                }

                Walk(prop.Signature.ReturnType, $"property '{type.FullName}::{prop.Name}'");
                foreach (var p in prop.Signature.ParameterTypes)
                {
                    Walk(p, $"property '{type.FullName}::{prop.Name}'");
                }
            }

            foreach (var method in type.Methods)
            {
                if (method.Signature is { } sig)
                {
                    Walk(sig.ReturnType, $"method '{method.FullName}' return");
                    foreach (var p in sig.ParameterTypes)
                    {
                        Walk(p, $"method '{method.FullName}' param");
                    }
                }

                if (method.CilMethodBody is null)
                {
                    continue;
                }

                foreach (var local in method.CilMethodBody.LocalVariables)
                {
                    Walk(local.VariableType, $"method '{method.FullName}' local #{local.Index}");
                }

                foreach (var instr in method.CilMethodBody.Instructions)
                {
                    switch (instr.Operand)
                    {
                        case TypeSpecification ts:
                            Walk(ts.Signature, $"method '{method.FullName}' instruction operand");
                            break;
                        case MemberReference { DeclaringType: TypeSpecification mts }:
                            Walk(mts.Signature, $"method '{method.FullName}' member-ref operand");
                            break;
                    }
                }
            }
        }

        return issues;
    }

    private void CheckGenericInstance(
        GenericInstanceTypeSignature gen,
        string context,
        List<ValidationIssue> issues
    )
    {
        // We can only check arity if the open generic type resolves to a definition we own.
        if (!gen.GenericType.TryResolve(dataProvider.Context, out var typeDef))
        {
            return;
        }

        var expected = typeDef.GenericParameters.Count;
        var actual = gen.TypeArguments.Count;

        if (expected != actual)
        {
            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Error,
                    Name,
                    $"Generic instance '{gen.GenericType.FullName}' on {context} has {actual} type "
                        + $"argument(s) but the definition declares {expected} generic parameter(s)"
                )
            );
        }
    }
}
