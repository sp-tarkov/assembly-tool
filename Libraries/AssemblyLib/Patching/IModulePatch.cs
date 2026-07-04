namespace AssemblyLib.Patching;

/// <summary>
///     Modules patches should be used when you need to change more than just the method body. Requires explicit IL instructions
/// </summary>
public interface IModulePatch
{
    bool Enabled { get; }
    void Patch();
}
