using System.Linq;
using AsmResolver.DotNet;

namespace AssemblyLib.Dumper;

public static class DumpyReflectionHelper
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
    public static bool GetValidateCertType(TypeDefinition type)
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

    public static bool GetMenuscreenType(TypeDefinition type)
    {
        return type.Name == "MenuScreen";
    }

    public static bool GetBackRequestMethod(MethodDefinition method)
    {
        return method.Parameters.Any(p => p.Name is "backRequest") && method.Parameters.Any(p => p.Name is "bResponse");
    }

    public static bool GetValidateCertMethods(MethodDefinition method)
    {
        return method.Name == "ValidateCertificate";
    }

    public static bool GetRunValidationMethod(MethodDefinition method)
    {
        return method.Name == "RunValidation";
    }

    public static bool GetRunValidationNextMethod(MethodDefinition method)
    {
        return method.Name == "MoveNext";
    }

    public static bool GetMenuscreenMethod(MethodDefinition method)
    {
        return method.Name == "Awake";
    }

    public static bool GetEnsureConMethod(MethodDefinition method)
    {
        return method.Name == "EnsureConsistency";
    }

    public static bool GetEnsureConSingleMethod(MethodDefinition method)
    {
        return method.Name == "EnsureConsistencySingle";
    }
}