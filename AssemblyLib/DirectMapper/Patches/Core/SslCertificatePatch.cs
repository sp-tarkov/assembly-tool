using AssemblyLib.Exceptions;
using AssemblyLib.Helpers;
using EFT;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.DirectMapper.Patches.Core;

[Injectable]
public class SslCertificatePatch(ModuleMemberLookup lookup, PatchHelper patchHelper) : IPatch
{
    public bool Enabled => true;

    public void Patch()
    {
        var moveNextMethod = lookup.Method<ClientCertificateHandler>("ValidateCertificate", typeof(byte[]));
        if (moveNextMethod?.CilMethodBody is null)
        {
            throw new FailedToFindTypeException(
                "Could not find `EFT.ClientCertificateHandler.ValidateCertificate()` when patching"
            );
        }

        patchHelper.NukeBoolBody(moveNextMethod.CilMethodBody, true);
    }
}
