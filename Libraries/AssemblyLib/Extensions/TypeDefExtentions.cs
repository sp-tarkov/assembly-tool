using AsmResolver.DotNet;

namespace AssemblyLib.Extensions;

internal static class TypeDefinitionExtensions
{
    extension(TypeDefinition typeDef)
    {
        /// <summary>
        ///     Is this type static
        /// </summary>
        /// <returns>True if static</returns>
        public bool IsStatic()
        {
            return typeDef.IsAbstract && typeDef.IsSealed;
        }

        /// <summary>
        ///     Is this type a struct
        /// </summary>
        /// <returns>true if struct</returns>
        public bool IsStruct()
        {
            return typeDef.IsValueType && !typeDef.IsEnum;
        }

        /// <summary>
        ///     Is this type a game object 'UnityEngine.Object' or 'Sirenix.OdinInspector.SerializedMonoBehaviour'
        /// </summary>
        /// <returns>true if GameObject</returns>
        public bool IsGameObject()
        {
            return typeDef.InheritsFrom("UnityEngine", "Object")
                   || typeDef.InheritsFrom("Sirenix.OdinInspector", "SerializedMonoBehaviour");
        }

        /// <summary>
        ///     Is this type in a namespace
        /// </summary>
        /// <returns>true if in namespace</returns>
        public bool IsInNamespace()
        {
            return typeDef.FullName.Contains('.');
        }

        /// <summary>
        ///     Is this an empty type
        /// </summary>
        /// <returns>true if no methods, props, fields, events, or nested types</returns>
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
