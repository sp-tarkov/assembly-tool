using AsmResolver.DotNet;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Dumper;

[Injectable]
public class DumpyReflectionHelper()
{
    /// <summary>
    /// <para>Gets the type that has a method called SendAndHandleRetries.</para>
    /// <para>This type is the only one with method.</para>
    /// </summary>
    /// <param name="type">TypeDefinition</param>
    /// <returns>boolean</returns>
    public bool GetBackRequestType(TypeDefinition type)
    {
        return type.Methods.Any(m => m.Name == "SendAndHandleRetries");
    }

    /// <summary>
    /// <para>Gets the type that has a method called ValidateCertificate as the name.</para>
    /// </summary>
    /// <param name="type">TypeDefinition</param>
    /// <returns>boolean</returns>
    public bool GetValidateCertType(TypeDefinition type)
    {
        return type.Methods.Any(m => m.Name == "ValidateCertificate");
    }

    /// <summary>
    /// <para>Gets the type that has a method called RunValidation as the name.</para>
    /// </summary>
    /// <param name="type">TypeDefinition</param>
    /// <returns>boolean</returns>
    public bool GetRunValidationType(TypeDefinition type)
    {
        return type.Methods.Any(m => m.Name == "RunValidation");
    }

    /// <summary>
    /// <para>Gets the type that has ConsistencyController as the name.</para>
    /// <para>FilesChecker.dll is not obfuscated.</para>
    /// </summary>
    /// <param name="type">TypeDefinition</param>
    /// <returns>boolean</returns>
    public bool GetEnsureConsistencyType(TypeDefinition type)
    {
        return type.Name == "ConsistencyController";
    }

    public bool GetMenuscreenType(TypeDefinition type)
    {
        return type.Name == "MenuScreen";
    }

    public bool GetBackRequestMethod(MethodDefinition method)
    {
        return method.Parameters.Any(p => p.Name is "backRequest")
            && method.Parameters.Any(p => p.Name is "bResponse");
    }

    public bool GetValidateCertMethods(MethodDefinition method)
    {
        return method.Name == "ValidateCertificate";
    }

    public bool GetRunValidationMethod(MethodDefinition method)
    {
        return method.Name == "RunValidation";
    }

    public bool GetRunValidationNextMethod(MethodDefinition method)
    {
        return method.Name == "MoveNext";
    }

    public bool GetMenuscreenMethod(MethodDefinition method)
    {
        return method.Name == "Awake";
    }

    public bool GetEnsureConMethod(MethodDefinition method)
    {
        return method.Name == "EnsureConsistency";
    }

    public bool GetEnsureConSingleMethod(MethodDefinition method)
    {
        return method.Name == "EnsureConsistencySingle";
    }
}
