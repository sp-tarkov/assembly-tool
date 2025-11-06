using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using AssemblyLib.Extensions;
using AssemblyLib.Shared;
using Serilog;
using Serilog.Events;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Remapper;

[Injectable]
public sealed class Publicizer(DataProvider dataProvider, Statistics stats)
{
    /// <summary>
    /// Publicize the provided type
    /// </summary>
    /// <param name="type">Type to publicize</param>
    /// <returns>List of fields that should be renamed</returns>
    public List<FieldDefinition> PublicizeType(TypeDefinition type)
    {
        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Publicizing Type [{Utf8String}]", type.Name?.ToString());
        }

        if (
            type is { IsNested: false, IsPublic: false } or { IsNested: true, IsNestedPublic: false }
            && type.Interfaces.All(i => i.Interface?.Name != "IEffect")
        )
        {
            type.Attributes &= ~TypeAttributes.VisibilityMask; // Remove all visibility mask attributes
            type.Attributes |= type.IsNested ? TypeAttributes.NestedPublic : TypeAttributes.Public; // Apply a public visibility attribute
            stats.TypePublicizedCount++;
        }

        if (type.IsSealed)
        {
            type.Attributes &= ~TypeAttributes.Sealed; // Remove the Sealed attribute if it exists
        }

        foreach (var method in type.Methods)
        {
            PublicizeMethod(method);
        }

        foreach (var property in type.Properties)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug(
                    "Publicizing Property [{PropertyDeclaringType}::{PropertyName}]",
                    property.DeclaringType,
                    property.Name?.ToString()
                );
            }

            // TODO: This is hacky but works for now, find a better solution. Need to check MD tokens to build associations,
            // this is a problem for later me.

            // NOTE: Ignore properties that are interface impls that are private.
            // This causes issues with json deserialization in the server.
            if (property.Name?.Contains(".") ?? false)
            {
                continue;
            }

            if (property.GetMethod != null)
            {
                PublicizeMethod(property.GetMethod);
            }

            if (property.SetMethod != null)
            {
                PublicizeMethod(property.SetMethod);
            }

            stats.PropertyPublicizedCount++;
        }

        return PublicizeFields(type);
    }

    private void PublicizeMethod(MethodDefinition method)
    {
        if (method.IsCompilerControlled || method.IsPublic)
        {
            return;
        }

        if (
            (method.Name?.StartsWith("GInterface") ?? false)
            && dataProvider.Settings.InterfaceMethodsToIgnore.Any(ignoredMethod => method.Name.EndsWith(ignoredMethod))
        )
        {
            Log.Information(
                "Not publicizing {FullName}::{MethodName} due to it being ignored",
                method.DeclaringType!.FullName,
                method.Name.ToString()
            );
            return;
        }

        // Workaround to not publicize a specific method so the game doesn't crash
        if (method.Name == "TryGetScreen")
        {
            return;
        }

        method.Attributes &= ~MethodAttributes.MemberAccessMask;
        method.Attributes |= MethodAttributes.Public;

        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug(
                "Publicizing Method [{MethodDeclaringType}::{MethodName}]",
                method.DeclaringType,
                method.Name?.ToString()
            );
        }

        stats.MethodPublicizedCount++;
    }

    private List<FieldDefinition> PublicizeFields(TypeDefinition type)
    {
        // We only publicize fields that are serialized on GameObjects
        if (type.IsGameObject())
        {
            foreach (var field in type.Fields)
            {
                if (!field.IsPublic && !field.IsEventField() && field.IsUnitySerializedField())
                {
                    field.PublicizeField();
                    stats.FieldPublicizedCount++;
                }
            }

            // We don't rename anything on GameObjects, this breaks unity, return an empty list.
            return [];
        }

        var fieldsToRename = new List<FieldDefinition>();
        foreach (var field in type.Fields)
        {
            if (field.IsPublic || field.IsEventField())
            {
                continue;
            }

            field.PublicizeField();
            fieldsToRename.Add(field);
            stats.FieldPublicizedCount++;

            if (
                field.HasCustomAttribute("UnityEngine", "SerializeField")
                || field.HasCustomAttribute("Newtonsoft.Json", "JsonPropertyAttribute")
            )
            {
                continue;
            }

            // This field isn't meant to be serialized, make sure we don't serialize it
            field.Attributes |= FieldAttributes.NotSerialized;
        }

        return fieldsToRename;
    }
}
