namespace AssemblyLib.Validation;

public enum ValidationStage
{
    /// <summary>Runs before remapping — validates mapping configuration.</summary>
    PreMapping,

    /// <summary>Runs after remapping — validates the resulting assembly state.</summary>
    PostMapping,
}
