using AsmResolver.DotNet;

namespace AssemblyLib.Extensions;

internal static class TypeDefinitionExtensions
{
    extension(TypeDefinition typeDef)
    {
        public bool IsStatic()
        {
            return typeDef.IsAbstract && typeDef.IsSealed;
        }

        public bool IsStruct()
        {
            return typeDef.IsValueType && !typeDef.IsEnum;
        }

        public bool IsGameObject()
        {
            return typeDef.InheritsFrom("UnityEngine", "Object")
                   || typeDef.InheritsFrom("Sirenix.OdinInspector", "SerializedMonoBehaviour");
        }

        public bool IsInNamespace()
        {
            return typeDef.FullName.Contains('.');
        }

        public bool IsEmptyType()
        {
            return typeDef.Methods.Count(t => !t.IsConstructor) == 0
                   && typeDef.Properties.Count == 0
                   && typeDef.Fields.Count == 0
                   && typeDef.Events.Count == 0
                   && typeDef.NestedTypes.Count == 0;
        }
    }
}
