namespace AssemblyLib.DirectMapper.Patches;

/// <summary>
///     Patches are the last thing to run and should use the remapped name, not the obfuscated name.
/// </summary>
public interface IPatch
{
    bool Enabled { get; }
    void Patch();
}
