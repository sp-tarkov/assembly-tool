using System.Linq;
using dnlib.DotNet;

namespace ReCodeItLib.Dumper;

public static class DumpyReflectionHelper
{
    /// <summary>
    /// <para>Gets the type that has a method called SendAndHandleRetries.</para>
    /// <para>This type is the only one with method.</para>
    /// </summary>
    /// <param name="type">TypeDefinition</param>
    /// <returns>boolean</returns>
    public static bool GetBackRequestType(TypeDef type)
    {
        return type.Methods.Any(m => m.Name == "SendAndHandleRetries");
    }

    /// <summary>
    /// <para>Gets the type that has a method called ValidateCertificate as the name.</para>
    /// </summary>
    /// <param name="type">TypeDefinition</param>
    /// <returns>boolean</returns>
    public static bool GetValidateCertType(TypeDef type)
    {
        return type.Methods.Any(m => m.Name == "ValidateCertificate");
    }

    /// <summary>
    /// <para>Gets the type that has a method called RunValidation as the name.</para>
    /// </summary>
    /// <param name="type">TypeDefinition</param>
    /// <returns>boolean</returns>
    public static bool GetRunValidationType(TypeDef type)
    {
        return type.Methods.Any(m => m.Name == "RunValidation");
    }

    /// <summary>
    /// <para>Gets the type that has ConsistencyController as the name.</para>
    /// <para>FilesChecker.dll is not obfuscated.</para>
    /// </summary>
    /// <param name="type">TypeDefinition</param>
    /// <returns>boolean</returns>
    public static bool GetEnsureConsistencyType(TypeDef type)
    {
        return type.Name == "ConsistencyController";
    }

    public static bool GetMenuscreenType(TypeDef type)
    {
        return type.Name == "MenuScreen";
    }

    public static bool GetBackRequestMethod(MethodDef method)
    {
        return method.Parameters.Any(p => p.Name is "backRequest") && method.Parameters.Any(p => p.Name is "bResponse");
    }

    public static bool GetValidateCertMethods(MethodDef method)
    {
        return method.Name == "ValidateCertificate";
    }

    public static bool GetRunValidationMethod(MethodDef method)
    {
        return method.Name == "RunValidation";
    }

    public static bool GetRunValidationNextMethod(MethodDef method)
    {
        return method.Name == "MoveNext";
    }

    public static bool GetMenuscreenMethod(MethodDef method)
    {
        return method.Name == "Awake";
    }

    public static bool GetEnsureConMethod(MethodDef method)
    {
        return method.Name == "EnsureConsistency";
    }

    public static bool GetEnsureConSingleMethod(MethodDef method)
    {
        return method.Name == "EnsureConsistencySingle";
    }
}