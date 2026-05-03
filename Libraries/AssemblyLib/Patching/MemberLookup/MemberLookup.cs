using SPTarkov.DI.Annotations;

namespace AssemblyLib.Patching.MemberLookup;

[Injectable(InjectionType.Singleton)]
public sealed class MemberLookup(EftMemberLookup eftLookup, StubMemberLookup stubLookup)
{
    public AbstractMemberLookup Eft => eftLookup;
    public AbstractMemberLookup Stub => stubLookup;
}
