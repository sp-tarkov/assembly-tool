using AsmResolver.DotNet;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Validation.Validators;

/// <summary>
///     Post-mapping validator. Detects name collisions that would produce an invalid or unverifiable assembly:
///     duplicate type names within a namespace, and duplicate member signatures within a type.
/// </summary>
[Injectable]
public class NameCollisionValidator(ILogger<NameCollisionValidator> logger, DataProvider dataProvider)
    : IAssemblyValidator
{
    public string Name => "Name Collision";
    public bool Enabled => true;
    public int Priority => 10;
    public ValidationStage Stage => ValidationStage.PostMapping;

    public IReadOnlyList<ValidationIssue> Validate()
    {
        var issues = new List<ValidationIssue>();

        if (dataProvider.LoadedModule is null)
        {
            return issues;
        }

        var allTypes = dataProvider.LoadedModule.GetAllTypes().ToList();

        CheckTypeCollisions(allTypes, issues);

        foreach (var type in allTypes)
        {
            CheckMethodCollisions(type, issues);
            CheckFieldCollisions(type, issues);
            CheckPropertyCollisions(type, issues);
        }

        return issues;
    }

    private void CheckTypeCollisions(IList<TypeDefinition> allTypes, List<ValidationIssue> issues)
    {
        // Top-level: collide when (namespace, name) matches
        var topLevel = allTypes
            .Where(t => t.DeclaringType is null)
            .GroupBy(t => (Ns: t.Namespace?.ToString() ?? "", Name: t.Name?.ToString() ?? ""))
            .Where(g => g.Count() > 1);

        foreach (var g in topLevel)
        {
            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Error,
                    Name,
                    $"Duplicate top-level type '{g.Key.Ns}.{g.Key.Name}' ({g.Count()} definitions)"
                )
            );
        }

        // Nested: collide when (declaring type, name) matches
        var nested = allTypes
            .Where(t => t.DeclaringType is not null)
            .GroupBy(t => (Parent: t.DeclaringType!.FullName, Name: t.Name?.ToString() ?? ""))
            .Where(g => g.Count() > 1);

        foreach (var g in nested)
        {
            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Error,
                    Name,
                    $"Duplicate nested type '{g.Key.Name}' inside '{g.Key.Parent}' ({g.Count()} definitions)"
                )
            );
        }
    }

    private void CheckMethodCollisions(TypeDefinition type, List<ValidationIssue> issues)
    {
        var duplicates = type
            .Methods.Where(m => !m.IsSpecialName)
            .GroupBy(m => (Name: m.Name?.ToString() ?? "", Params: GetParamKey(m), Generics: m.GenericParameters.Count))
            .Where(g => g.Count() > 1);

        foreach (var g in duplicates)
        {
            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Error,
                    Name,
                    $"Duplicate method '{g.Key.Name}({g.Key.Params})' on '{type.FullName}'"
                )
            );
        }
    }

    private void CheckFieldCollisions(TypeDefinition type, List<ValidationIssue> issues)
    {
        var duplicates = type.Fields.GroupBy(f => f.Name?.ToString() ?? "").Where(g => g.Count() > 1);

        foreach (var g in duplicates)
        {
            issues.Add(
                new ValidationIssue(ValidationSeverity.Error, Name, $"Duplicate field '{g.Key}' on '{type.FullName}'")
            );
        }
    }

    private void CheckPropertyCollisions(TypeDefinition type, List<ValidationIssue> issues)
    {
        var duplicates = type
            .Properties.GroupBy(p => (Name: p.Name?.ToString() ?? "", Params: GetPropertyParamKey(p)))
            .Where(g => g.Count() > 1);

        foreach (var g in duplicates)
        {
            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Error,
                    Name,
                    $"Duplicate property '{g.Key.Name}' on '{type.FullName}'"
                )
            );
        }
    }

    private static string GetParamKey(MethodDefinition method)
    {
        if (method.Signature is null)
        {
            return string.Empty;
        }

        return string.Join(",", method.Signature.ParameterTypes.Select(p => p.FullName));
    }

    private static string GetPropertyParamKey(PropertyDefinition property)
    {
        if (property.Signature is null)
        {
            return string.Empty;
        }

        // Indexed properties have parameter types in their signature
        return string.Join(",", property.Signature.ParameterTypes.Select(p => p.FullName));
    }
}
