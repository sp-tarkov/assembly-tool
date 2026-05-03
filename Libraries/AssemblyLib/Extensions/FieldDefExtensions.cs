using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;

namespace AssemblyLib.Extensions;

internal static class FieldDefExtensions
{
    /// <param name="field">Field to check</param>
    extension(FieldDefinition field)
    {
        /// <summary>
        ///     Is this a unity serialized field
        /// </summary>
        /// <returns>True if serialized field</returns>
        public bool IsUnitySerializedField()
        {
            return field.HasCustomAttribute("UnityEngine", "SerializeField");
        }

        /// <summary>
        ///     Publicizes field and removes readonly
        /// </summary>
        public void PublicizeField()
        {
            // Remove all visibility mask attributes
            field.Attributes &= ~FieldAttributes.FieldAccessMask;
            // Apply a public visibility attribute
            field.Attributes |= FieldAttributes.Public;
            // Ensure the field is NOT readonly
            field.Attributes &= ~FieldAttributes.InitOnly;
        }

        /// <summary>
        ///     Is this field a newtonsoft serialized field
        /// </summary>
        /// <returns>true if newtonsoft json field</returns>
        public bool IsNewtonSoftProperty()
        {
            return field.HasCustomAttribute("Newtonsoft.Json", "JsonPropertyAttribute");
        }

        /// <summary>
        ///     Is this field a backing field for an event?
        /// </summary>
        /// <returns>True if backing field</returns>
        public bool IsEventField()
        {
            var declType = field.DeclaringType;

            foreach (var evt in declType?.Events ?? [])
            {
                if (evt.AddMethod is { CilMethodBody: not null })
                {
                    if (IsMemberReferenceNameMatch(evt.AddMethod.CilMethodBody.Instructions, field.Name))
                    {
                        return true;
                    }
                }

                if (evt.RemoveMethod is { CilMethodBody: not null })
                {
                    if (IsMemberReferenceNameMatch(evt.RemoveMethod.CilMethodBody.Instructions, field.Name))
                    {
                        return true;
                    }
                }

                if (evt.FireMethod is { CilMethodBody: not null })
                {
                    if (IsMemberReferenceNameMatch(evt.FireMethod.CilMethodBody.Instructions, field.Name))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool IsBackingField()
        {
            var declaringType = field.DeclaringType;
            if (declaringType is null)
            {
                return false;
            }

            foreach (var property in declaringType.Properties)
            {
                if (
                    property.GetMethod != null && ReferencesField(property.GetMethod, field)
                    || property.SetMethod != null && ReferencesField(property.SetMethod, field)
                )
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    ///     Is the field referenced inside the methods instructions
    /// </summary>
    /// <param name="instructions">Instructions to check</param>
    /// <param name="memberName">Member name to look for</param>
    /// <returns>True if field is referenced in the method</returns>
    private static bool IsMemberReferenceNameMatch(CilInstructionCollection instructions, Utf8String? memberName)
    {
        foreach (var instr in instructions)
        {
            if (instr.Operand is FieldDefinition fieldDefinition && fieldDefinition.Name == memberName)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ReferencesField(MethodDefinition method, FieldDefinition field)
    {
        if (method.CilMethodBody == null)
        {
            return false;
        }

        foreach (var instruction in method.CilMethodBody.Instructions)
        {
            if (instruction.Operand is FieldDefinition fd && fd == field)
            {
                return true;
            }

            if (instruction.Operand is MemberReference mr &&
                mr.Name == field.Name &&
                mr.DeclaringType?.FullName == field.DeclaringType?.FullName)
            {
                return true;
            }
        }

        return false;
    }
}
