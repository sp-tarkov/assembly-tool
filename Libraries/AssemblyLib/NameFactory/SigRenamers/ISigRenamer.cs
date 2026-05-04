using AsmResolver.DotNet;

namespace AssemblyLib.NameFactory.SigRenamers;

public interface ISigRenamer
{
    int Priority { get; }
    bool Enabled { get; }
    ERenamerType Type { get; }
    void Rename(TypeDefinition targetType, TypeDefinition dummyType);
}
