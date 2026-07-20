using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Metadata.Tables;
using SPTarkov.DI.Annotations;

namespace AssemblyLib.Helpers;

[Injectable]
public sealed class Publicizer(ILogger<Publicizer> logger, DataProvider dataProvider, Statistics stats)
{
    /// <summary>
    /// Publicize the provided type
    /// </summary>
    /// <param name="type">Type to publicize</param>
    /// <returns>List of fields that should be renamed</returns>
    public void PublicizeType(TypeDefinition type)
    {
        type.Attributes &= ~TypeAttributes.VisibilityMask; // Remove all visibility mask attributes
        type.Attributes |= type.IsNested ? TypeAttributes.NestedPublic : TypeAttributes.Public; // Apply a public visibility attribute
        stats.TypePublicizedCount++;

        // A static class is abstract and sealed. Unsealing it stops it being static, which breaks
        // any extension methods it holds, and nothing can inherit it anyway.
        if (type.IsSealed && !type.IsAbstract)
        {
            type.Attributes &= ~TypeAttributes.Sealed; // Remove the Sealed attribute if it exists
        }

        foreach (var method in type.Methods)
        {
            PublicizeMethod(method);
        }

        foreach (var property in type.Properties)
        {
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

        PublicizeFields(type);
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
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(
                    "Not publicizing {FullName}::{MethodName} due to it being ignored",
                    method.DeclaringType!.FullName,
                    method.Name.ToString()
                );
            }

            return;
        }

        method.Attributes &= ~MethodAttributes.MemberAccessMask;
        method.Attributes |= MethodAttributes.Public;

        stats.MethodPublicizedCount++;
    }

    /// <summary>
    ///     Does any base type declare a member under this name? If so the field must stay private.
    /// </summary>
    private bool ShadowsPublicBaseMember(TypeDefinition type, string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        var current = type.BaseType;
        var guard = 0;

        while (current is not null && guard++ < 32)
        {
            if (!current.TryResolve(dataProvider.Context, out var baseType))
            {
                return false;
            }

            if (baseType.Fields.Any(f => f.Name == name) || baseType.Properties.Any(p => p.Name == name))
            {
                return true;
            }

            current = baseType.BaseType;
        }

        return false;
    }

    private void PublicizeFields(TypeDefinition type)
    {
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

            // We don't rename anything on GameObjects, this breaks unity.
            return;
        }

        foreach (var field in type.Fields)
        {
            if (field.IsPublic || field.IsEventField())
            {
                continue;
            }

            if (ShadowsPublicBaseMember(type, field.Name?.ToString()))
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation(
                        "Not publicizing {type}::{field}, it shadows a public base member",
                        type.FullName,
                        field.Name?.ToString()
                    );
                }

                continue;
            }

            field.PublicizeField();
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
    }
}
