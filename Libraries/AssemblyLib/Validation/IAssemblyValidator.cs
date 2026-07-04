namespace AssemblyLib.Validation;

public interface IAssemblyValidator
{
    string Name { get; }
    bool Enabled { get; }
    int Priority { get; }
    ValidationStage Stage { get; }
    IReadOnlyList<ValidationIssue> Validate();
}
