using AsmResolver.DotNet;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.NameFactory;

[Injectable(InjectionType.Singleton)]
public class DirectRenameCache
{
    private readonly HashSet<IMemberDefinition> _renamedMembers = new(ReferenceEqualityComparer.Instance);

    public void Add(IMemberDefinition member) => _renamedMembers.Add(member);

    public bool Contains(IMemberDefinition member) => _renamedMembers.Contains(member);
}
