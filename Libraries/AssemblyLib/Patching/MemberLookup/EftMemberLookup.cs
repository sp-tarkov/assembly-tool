using AsmResolver.DotNet;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Patching.MemberLookup;

[Injectable]
public sealed class EftMemberLookup(DataProvider dataProvider) : AbstractMemberLookup()
{
    protected override ModuleDefinition TargetModule => dataProvider.LoadedModule!;
}
