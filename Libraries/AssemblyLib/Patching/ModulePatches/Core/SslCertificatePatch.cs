using EFT;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Patching.ModulePatches.Core;

[Injectable]
public class SslCertificatePatch(MemberLookup.ModuleMemberLookup lookup, MethodBodyNuker methodBodyNuker) : IModulePatch
{
    public bool Enabled => true;

    public void Patch()
    {
        var moveNextMethod = lookup.Eft.Method<ClientCertificateHandler>("ValidateCertificate", typeof(byte[]));
        if (moveNextMethod?.CilMethodBody is null)
        {
            throw new FailedToFindTypeException(
                "Could not find `EFT.ClientCertificateHandler.ValidateCertificate()` when patching"
            );
        }

        methodBodyNuker.NukeBoolBody(moveNextMethod.CilMethodBody, true);
    }
}
