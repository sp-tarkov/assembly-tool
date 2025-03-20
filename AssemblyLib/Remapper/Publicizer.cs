using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;
using AssemblyLib.Application;
using AssemblyLib.Utils;

namespace AssemblyLib.ReMapper;

internal sealed class Publicizer(List<TypeDefinition> types, Statistics stats) 
    : IComponent
{
    public async Task StartPublicizeTypesTask()
    {
        var publicizeTasks = new List<Task>();
        
        foreach (var type in types)
        {
            publicizeTasks.Add(
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        Context.Instance.Get<Publicizer>()
                            !.PublicizeType(type);
                    }
                    catch (Exception ex)
                    {
                        Logger.QueueTaskException($"Exception in task: {ex.Message}");
                    }
                })
            );
        }

        if (DataProvider.Settings.DebugLogging)
        {
            await Task.WhenAll(publicizeTasks.ToArray());
            return;
        }
        
        await Logger.DrawProgressBar(publicizeTasks, "Publicizing Types");
    }
    
    private void PublicizeType(TypeDefinition type)
    {
        if (type is { IsNested: false, IsPublic: false } or { IsNested: true, IsNestedPublic: false }
            && type.Interfaces.All(i => i.Interface.Name != "IEffect"))
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
            if (property.GetMethod != null) PublicizeMethod(property.GetMethod, true);
            if (property.SetMethod != null) PublicizeMethod(property.SetMethod, true);

            stats.PropertyPublicizedCount++;
        }
        
        PublicizeFields(type);
    }

    private void PublicizeMethod(MethodDefinition method, bool isProperty = false)
    {
        if (method.IsCompilerControlled)
        {
            return;
        }

        if (method.IsPublic) return;
        
        // Workaround to not publicize a specific method so the game doesn't crash
        if (method.Name == "TryGetScreen") return;

        method.Attributes &= ~MethodAttributes.MemberAccessMask;
        method.Attributes |= MethodAttributes.Public;

        if (isProperty) return;

        stats.MethodPublicizedCount++;
    }

    private void PublicizeFields(TypeDefinition type)
    {
        var declType = type.IsNested ? type.DeclaringType : type;
        while (declType is
               {
                   FullName:
                   not null
                   and not "UnityEngine.Object"
                   and not "Sirenix.OdinInspector.SerializedMonoBehaviour"
               })
        { declType = declType.BaseType?.Resolve(); }
        
        if (declType?.FullName is "UnityEngine.Object" or "Sirenix.OdinInspector.SerializedMonoBehaviour")
        {
            return;
        }
            
        foreach (var field in type.Fields)
        {
            if (field.IsPublic /*|| IsEventField(type, field)*/) continue;
            
            stats.FieldPublicizedCount++;
            field.Attributes &= ~FieldAttributes.FieldAccessMask; // Remove all visibility mask attributes
            field.Attributes |= FieldAttributes.Public; // Apply a public visibility attribute
            
            // Ensure the field is NOT readonly
            field.Attributes &= ~FieldAttributes.InitOnly;
                
            if (field.HasCustomAttribute("UnityEngine", "SerializeField") ||
                field.HasCustomAttribute("Newtonsoft.Json", "JsonPropertyAttribute"))
                continue;
                
            // Make sure we don't serialize this field.
            field.Attributes |= FieldAttributes.NotSerialized;
                
            //Logger.LogSync($"Skipping {field.FullName} serialization");
        }
    }

    private static bool IsEventField(TypeDefinition type, FieldDefinition field)
    {
        foreach (var evt in type.Events)
        {
            if (evt.AddMethod is { CilMethodBody: not null })
            {
                foreach (var instr in evt.AddMethod.CilMethodBody.Instructions)
                {
                    if (instr.Operand is MemberReference memberRef && memberRef.Name == field.Name)
                    {
                        return true;
                    }
                }
            }
                
            if (evt.RemoveMethod is { CilMethodBody: not null })
            {
                foreach (var instr in evt.RemoveMethod.CilMethodBody.Instructions)
                {
                    if (instr.Operand is MemberReference memberRef && memberRef.Name == field.Name)
                    {
                        return true;
                    }
                }
            }
                
            if (evt.FireMethod is { CilMethodBody: not null })
            {
                foreach (var instr in evt.FireMethod.CilMethodBody.Instructions)
                {
                    if (instr.Operand is MemberReference memberRef && memberRef.Name == field.Name)
                    {
                        return true;
                    }
                }
            }
        }
        
        return false;
    }
}