namespace AssemblyLib.Validation;

public record ValidationIssue(ValidationSeverity Severity, string ValidatorName, string Message);