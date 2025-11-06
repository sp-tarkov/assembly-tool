using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using Serilog;
using Serilog.Events;

namespace AssemblyLib.Extensions;

internal static class FieldDefExtensions
{
    /// <summary>
    ///     Is this a unity serialized field
    /// </summary>
    /// <param name="field">Field to check</param>
    /// <returns>True if serialized field</returns>
    public static bool IsUnitySerializedField(this FieldDefinition field)
    {
        return field.HasCustomAttribute("UnityEngine", "SerializeField");
    }

    /// <summary>
    ///     Publicizes field and removes readonly
    /// </summary>
    /// <param name="field">Field to publicize</param>
    public static void PublicizeField(this FieldDefinition field)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug(
                "Publicizing Field [{FieldDeclaringType}::{Utf8String}]",
                field.DeclaringType,
                field.Name?.ToString()
            );
        }

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
    /// <param name="field">Field to check</param>
    /// <returns>true if newtonsoft json field</returns>
    public static bool IsNewtonSoftProperty(this FieldDefinition field)
    {
        return field.HasCustomAttribute("Newtonsoft.Json", "JsonPropertyAttribute");
    }

    /// <summary>
    ///     Is this field a backing field for an event?
    /// </summary>
    /// <param name="field">Field to check</param>
    /// <returns>True if backing field</returns>
    public static bool IsEventField(this FieldDefinition field)
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
}
