using System.Linq;
using Mono.Cecil;

namespace ReCodeItLib.Dumper;

public static class DumpyTypeHelper
{
    /// <summary>
    /// <para>Gets the type that has a method called SendAndHandleRetries.</para>
    /// <para>This type is the only one with method.</para>
    /// </summary>
    /// <param name="type">TypeDefinition</param>
    /// <returns>boolean</returns>
    public static bool GetBackRequestType(TypeDefinition type)
    {
        return type.Methods.Any(m => m.Name == "SendAndHandleRetries");
    }

    /// <summary>
    /// <para>Gets the type that has a method called ValidateCertificate as the name.</para>
    /// </summary>
    /// <param name="type">TypeDefinition</param>
    /// <returns>boolean</returns>
    public static bool GetValidateCertificateType(TypeDefinition type)
    {
        return type.Methods.Any(m => m.Name == "ValidateCertificate");
    }

    /// <summary>
    /// <para>Gets the type that has a method called RunValidation as the name.</para>
    /// </summary>
    /// <param name="type">TypeDefinition</param>
    /// <returns>boolean</returns>
    public static bool GetRunValidationType(TypeDefinition type)
    {
        return type.Methods.Any(m => m.Name == "RunValidation");
    }

    /// <summary>
    /// <para>Gets the type that has ConsistencyController as the name.</para>
    /// <para>FilesChecker.dll is not obfuscated.</para>
    /// </summary>
    /// <param name="type">TypeDefinition</param>
    /// <returns>boolean</returns>
    public static bool GetEnsureConsistencyType(TypeDefinition type)
    {
        return type.Name == "ConsistencyController";
    }

    public static bool GetMenuScreenType(TypeDefinition type)
    {
        return type.Name == "MenuScreen";
    }
}