using AsmResolver.DotNet;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Validation.Validators;

/// <summary>
///     Post-mapping validator. Catches names that would produce an invalid or unloadable assembly:
///     null/empty names, names that consist only of whitespace, and names containing characters
///     that are illegal in IL identifiers (null bytes, forward slashes outside nested-type notation).
/// </summary>
[Injectable]
public class InvalidNamingValidator(ILogger<InvalidNamingValidator> logger, DataProvider dataProvider)
    : IAssemblyValidator
{
    public string Name => "Invalid Naming";
    public bool Enabled => true;
    public int Priority => 15;
    public ValidationStage Stage => ValidationStage.PostMapping;

    public IReadOnlyList<ValidationIssue> Validate()
    {
        var issues = new List<ValidationIssue>();

        if (dataProvider.LoadedModule is null)
            return issues;

        foreach (var type in dataProvider.LoadedModule.GetAllTypes())
        {
            CheckTypeName(type, issues);

            foreach (var method in type.Methods)
            {
                CheckMemberName(method.Name?.ToString(), $"method on '{type.FullName}'", issues);
            }

            foreach (var field in type.Fields)
            {
                CheckMemberName(field.Name?.ToString(), $"field on '{type.FullName}'", issues);
            }

            foreach (var property in type.Properties)
            {
                CheckMemberName(property.Name?.ToString(), $"property on '{type.FullName}'", issues);
            }

            foreach (var evt in type.Events)
            {
                CheckMemberName(evt.Name?.ToString(), $"event on '{type.FullName}'", issues);
            }
        }

        return issues;
    }

    private void CheckTypeName(TypeDefinition type, List<ValidationIssue> issues)
    {
        var name = type.Name?.ToString();

        if (string.IsNullOrEmpty(name))
        {
            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Error,
                    Name,
                    $"Type in namespace '{type.Namespace}' has a null or empty name"
                )
            );
            return;
        }

        CheckIllegalCharacters(name, $"type '{type.FullName}'", issues);
    }

    private void CheckMemberName(string? name, string context, List<ValidationIssue> issues)
    {
        if (string.IsNullOrEmpty(name))
        {
            issues.Add(new ValidationIssue(ValidationSeverity.Error, Name, $"Empty or null name for {context}"));
            return;
        }

        // Compiler-generated members (e.g. <Main>b__0) are expected to contain angle brackets
        if (name.StartsWith('<'))
        {
            return;
        }

        CheckIllegalCharacters(name, context, issues);
    }

    private void CheckIllegalCharacters(string name, string context, List<ValidationIssue> issues)
    {
        if (name.Contains('\0'))
        {
            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Error,
                    Name,
                    $"Name of {context} contains a null byte: '{EscapeForLog(name)}'"
                )
            );
        }

        if (name.AsSpan().IndexOfAny('\r', '\n') >= 0)
        {
            issues.Add(
                new ValidationIssue(
                    ValidationSeverity.Error,
                    Name,
                    $"Name of {context} contains a newline character: '{EscapeForLog(name)}'"
                )
            );
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            issues.Add(new ValidationIssue(ValidationSeverity.Error, Name, $"Name of {context} is whitespace-only"));
        }
    }

    private static string EscapeForLog(string name) =>
        name.Replace("\0", "\\0").Replace("\r", "\\r").Replace("\n", "\\n");
}
