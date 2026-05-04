using AsmResolver.DotNet;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Validation.Validators;

/// <summary>
///     Post-mapping validator. Sanity-checks the AssemblyRef table on the rewritten module:
///     no entry should point back at the assembly itself, names must be non-empty, and no two
///     entries should share the same name (some host runtimes refuse to load an assembly with
///     duplicate AssemblyRef rows; AsmResolver does not deduplicate them automatically).
/// </summary>
[Injectable]
public class AssemblyReferenceValidator(
    ILogger<AssemblyReferenceValidator> logger,
    DataProvider dataProvider
) : IAssemblyValidator
{
    public string Name => "Assembly References";
    public bool Enabled => true;
    public int Priority => 25;
    public ValidationStage Stage => ValidationStage.PostMapping;

    public IReadOnlyList<ValidationIssue> Validate()
    {
        var issues = new List<ValidationIssue>();

        if (dataProvider.LoadedModule is null)
        {
            return issues;
        }

        var module = dataProvider.LoadedModule;
        var selfName = module.Assembly?.Name?.ToString();

        var byName = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var asmRef in module.AssemblyReferences)
        {
            var name = asmRef.Name?.ToString();

            if (string.IsNullOrWhiteSpace(name))
            {
                issues.Add(
                    new ValidationIssue(
                        ValidationSeverity.Error,
                        Name,
                        "AssemblyRef entry has a null or whitespace-only name"
                    )
                );
                continue;
            }

            if (selfName is not null && string.Equals(name, selfName, StringComparison.Ordinal))
            {
                issues.Add(
                    new ValidationIssue(
                        ValidationSeverity.Error,
                        Name,
                        $"Self-referential AssemblyRef '{name}' is still present in the module — "
                            + "AssemblyWriter.RemoveSelfAssemblyReferences should have stripped it"
                    )
                );
            }

            byName[name] = byName.TryGetValue(name, out var c) ? c + 1 : 1;
        }

        foreach (var (name, count) in byName)
        {
            if (count > 1)
            {
                issues.Add(
                    new ValidationIssue(
                        ValidationSeverity.Error,
                        Name,
                        $"AssemblyRef '{name}' has {count} duplicate entries"
                    )
                );
            }
        }

        // Surface any TypeRef whose AssemblyReference scope is no longer present in
        // module.AssemblyReferences — AsmResolver will silently re-add it on write,
        // re-introducing rows we thought we removed.
        var asmRefSet = new HashSet<AssemblyReference>(module.AssemblyReferences, ReferenceEqualityComparer.Instance);

        foreach (var typeRef in module.GetImportedTypeReferences())
        {
            if (typeRef.Scope is AssemblyReference asmRef && !asmRefSet.Contains(asmRef))
            {
                issues.Add(
                    new ValidationIssue(
                        ValidationSeverity.Warning,
                        Name,
                        $"TypeRef '{typeRef.FullName}' has Scope = orphaned AssemblyRef '{asmRef.Name}' "
                            + "that is no longer in the module's AssemblyReferences collection — "
                            + "this row will be re-emitted on write"
                    )
                );
            }
        }

        return issues;
    }
}
