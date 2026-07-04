using AssemblyLib.Models;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Validation.Validators;

/// <summary>
///     Pre-mapping validator. Checks that no two entries in the mapping config resolve to the same output name,
///     which would produce an ambiguous or colliding type after remapping.
/// </summary>
[Injectable]
public class DuplicateMappingValidator(ILogger<DuplicateMappingValidator> logger, DataProvider dataProvider)
    : IAssemblyValidator
{
    public string Name => "Duplicate Mapping";
    public bool Enabled => true;
    public int Priority => 20;
    public ValidationStage Stage => ValidationStage.PreMapping;

    public IReadOnlyList<ValidationIssue> Validate()
    {
        var issues = new List<ValidationIssue>();

        // Top-level types must have globally unique namespace-qualified names + arity.
        // GClass3074 and GClass3075`1 both mapping to "Foo" is valid — they resolve to "Foo" and "Foo`1".
        var topLevelSeen = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (obfName, model) in dataProvider.DirectMapModels)
        {
            if (model.NewName is { } newName)
            {
                var baseName = string.IsNullOrEmpty(model.NewNamespace) ? newName : $"{model.NewNamespace}.{newName}";

                var key = WithArity(baseName, obfName);

                if (!topLevelSeen.TryAdd(key, obfName))
                {
                    issues.Add(
                        new ValidationIssue(
                            ValidationSeverity.Error,
                            Name,
                            $"'{baseName}' is the mapped output of both '{topLevelSeen[key]}' and '{obfName}'"
                        )
                    );
                }
            }

            if (model.NestedTypes is not null)
            {
                CheckNestedTypes(model.NestedTypes, issues);
            }
        }

        return issues;
    }

    // Nested types are scoped to their parent — use a fresh dict per parent so two different
    // parents can legitimately share the same nested type name (e.g. both having a nested Logger).
    // Arity is included in the key so a non-generic and a generic nested type with the same name are distinct.
    private void CheckNestedTypes(Dictionary<string, DirectMapModel> nestedTypes, List<ValidationIssue> issues)
    {
        var localSeen = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (obfName, model) in nestedTypes)
        {
            if (model.NewName is { } newName)
            {
                var key = WithArity(newName, obfName);
                if (!localSeen.TryAdd(key, obfName))
                {
                    issues.Add(
                        new ValidationIssue(
                            ValidationSeverity.Error,
                            Name,
                            $"Nested type name '{newName}' is used by both '{localSeen[key]}' and '{obfName}' within the same parent"
                        )
                    );
                }
            }

            if (model.NestedTypes is not null)
                CheckNestedTypes(model.NestedTypes, issues);
        }
    }

    /// <summary>
    ///     Appends the CLR generic arity suffix from the obfuscated name so that "Foo" (non-generic)
    /// and "Foo" sourced from "GClass123`1" (1 type param) produce distinct collision keys.
    /// </summary>
    private static string WithArity(string mappedName, string obfName)
    {
        var tick = obfName.LastIndexOf('`');
        if (tick >= 0 && int.TryParse(obfName.AsSpan(tick + 1), out var arity) && arity > 0)
        {
            return $"{mappedName}`{arity}";
        }

        return mappedName;
    }
}
