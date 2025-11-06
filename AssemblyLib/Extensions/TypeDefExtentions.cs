using AsmResolver.DotNet;

namespace AssemblyLib.Extensions;

internal static class TypeDefinitionExtensions
{
    public static bool IsStatic(this TypeDefinition typeDef)
    {
        return typeDef.IsAbstract && typeDef.IsSealed;
    }

    public static bool IsStruct(this TypeDefinition typeDef)
    {
        return typeDef.IsValueType && !typeDef.IsEnum;
    }

    public static bool IsGameObject(this TypeDefinition type)
    {
        return type.InheritsFrom("UnityEngine", "Object")
            || type.InheritsFrom("Sirenix.OdinInspector", "SerializedMonoBehaviour");
    }
}
