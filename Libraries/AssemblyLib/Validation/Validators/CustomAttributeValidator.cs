using AsmResolver.DotNet;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Validation.Validators;

/// <summary>
///     Post-mapping validator. Walks every <see cref="CustomAttribute"/> in the rewritten module
///     and verifies (a) the constructor reference is resolvable and (b) the attribute blob can
///     be deserialized without throwing.
///
///     AsmResolver deserializes attribute blobs lazily — a renamed type referenced inside an
///     attribute argument can leave the blob un-parseable until first access. This catches such
///     attributes before they explode at runtime in tooling that reads them.
/// </summary>
[Injectable]
public class CustomAttributeValidator(
    ILogger<CustomAttributeValidator> logger,
    DataProvider dataProvider
) : IAssemblyValidator
{
    public string Name => "Custom Attributes";
    public bool Enabled => true;
    public int Priority => 8;
    public ValidationStage Stage => ValidationStage.PostMapping;

    public IReadOnlyList<ValidationIssue> Validate()
    {
        var issues = new List<ValidationIssue>();

        if (dataProvider.LoadedModule is null)
        {
            return issues;
        }

        var module = dataProvider.LoadedModule;

        CheckProvider(module, "module", issues);

        if (module.Assembly is { } assembly)
        {
            CheckProvider(assembly, "assembly", issues);
        }

        foreach (var type in module.GetAllTypes())
        {
            CheckProvider(type, $"type '{type.FullName}'", issues);

            foreach (var field in type.Fields)
            {
                CheckProvider(field, $"field '{type.FullName}::{field.Name}'", issues);
            }

            foreach (var prop in type.Properties)
            {
                CheckProvider(prop, $"property '{type.FullName}::{prop.Name}'", issues);
            }

            foreach (var ev in type.Events)
            {
                CheckProvider(ev, $"event '{type.FullName}::{ev.Name}'", issues);
            }

            foreach (var method in type.Methods)
            {
                CheckProvider(method, $"method '{method.FullName}'", issues);

                foreach (var param in method.ParameterDefinitions)
                {
                    CheckProvider(param, $"parameter '{param.Name}' on '{method.FullName}'", issues);
                }
            }
        }

        return issues;
    }

    private void CheckProvider(IHasCustomAttribute target, string context, List<ValidationIssue> issues)
    {
        for (var i = 0; i < target.CustomAttributes.Count; i++)
        {
            var attr = target.CustomAttributes[i];

            // Constructor must resolve — without it the attribute is unconstructable at runtime.
            if (attr.Constructor is null)
            {
                issues.Add(
                    new ValidationIssue(
                        ValidationSeverity.Error,
                        Name,
                        $"Custom attribute #{i} on {context} has a null Constructor"
                    )
                );
                continue;
            }

            var ctorType = attr.Constructor.DeclaringType?.FullName ?? "<unknown>";

            // Same-assembly references should resolve. Cross-assembly resolution depends on the
            // runtime context being primed with every dependency, so a failed resolve there is
            // a soft warning rather than a hard error.
            var declScope = (attr.Constructor.DeclaringType as TypeReference)?.Scope;
            var isCrossAssembly = declScope is AssemblyReference;

            if (!attr.Constructor.TryResolve(dataProvider.Context, out _))
            {
                issues.Add(
                    new ValidationIssue(
                        isCrossAssembly ? ValidationSeverity.Warning : ValidationSeverity.Error,
                        Name,
                        $"Custom attribute on {context}: constructor '{ctorType}::.ctor' did not resolve"
                    )
                );
            }

            // Force the lazy blob to deserialise. If the blob references a type that was renamed
            // after the blob was last touched, .FixedArguments throws here.
            try
            {
                _ = attr.Signature?.FixedArguments.Count;
                _ = attr.Signature?.NamedArguments.Count;
            }
            catch (Exception ex)
            {
                issues.Add(
                    new ValidationIssue(
                        ValidationSeverity.Error,
                        Name,
                        $"Custom attribute '{ctorType}' on {context} has an undeserialisable blob: {ex.Message}"
                    )
                );
            }
        }
    }
}
