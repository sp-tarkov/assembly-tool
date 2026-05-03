using AssemblyLib.Exceptions;
using AssemblyLib.Helpers;
using AssemblyLib.Patching;
using AssemblyLib.Patching.MemberLookup;
using EFT;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Patches.Core;

[Injectable]
public class SslCertificatePatch(MemberLookup lookup, MethodBodyNuker methodBodyNuker) : IPatch
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
