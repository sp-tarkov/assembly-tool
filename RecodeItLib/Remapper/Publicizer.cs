using dnlib.DotNet;
using ReCodeItLib.Utils;

namespace ReCodeItLib.ReMapper;

internal class Publicizer
{
    public void PublicizeClasses(ModuleDefMD definition, bool isLauncher = false)
    {
        var types = definition.GetTypes();
        
        var publicizeTasks = new List<Task>(types.Count(t => !t.IsNested));
        foreach (var type in types)
        {
            if (type.IsNested) continue; // Nested types are handled when publicizing the parent type
            
            publicizeTasks.Add(
                Task.Factory.StartNew(() =>
                {
                    PublicizeType(type, isLauncher);
                })
            );
        }
        
        // TODO: This is broken. No idea why.
        while (!publicizeTasks.TrueForAll(t => t.Status == TaskStatus.RanToCompletion))
        {
            Logger.DrawProgressBar(publicizeTasks.Count(t => t.IsCompleted), publicizeTasks.Count, 50);
        }
        
        Task.WaitAll(publicizeTasks.ToArray());
    }

    private static void PublicizeType(TypeDef type, bool isLauncher)
    {
        // if (type.CustomAttributes.Any(a => a.AttributeType.Name ==
        // nameof(CompilerGeneratedAttribute))) { return; }
        
        if (!type.IsNested && !type.IsPublic || type.IsNested && !type.IsNestedPublic)
        {
            if (!type.Interfaces.Any(i => i.Interface.Name == "IEffect"))
            {
                type.Attributes &= ~TypeAttributes.VisibilityMask; // Remove all visibility mask attributes
                type.Attributes |= type.IsNested ? TypeAttributes.NestedPublic : TypeAttributes.Public; // Apply a public visibility attribute
            }
        }
        
        if (type.IsSealed && !isLauncher)
        {
            type.Attributes &= ~TypeAttributes.Sealed; // Remove the Sealed attribute if it exists
        }
        
        foreach (var method in type.Methods)
        {
            PublicizeMethod(method);
        }
        
        foreach (var property in type.Properties)
        {
            if (property.GetMethod != null) PublicizeMethod(property.GetMethod);
            if (property.SetMethod != null) PublicizeMethod(property.SetMethod);
        }
        
        foreach (var nestedType in type.NestedTypes)
        {
            PublicizeType(nestedType, isLauncher);
        }
    }

    private static void PublicizeMethod(MethodDef method)
    {
        if (method.IsCompilerControlled /*|| method.CustomAttributes.Any(a => a.AttributeType.Name == nameof(CompilerGeneratedAttribute))*/)
        {
            return;
        }

        if (method.IsPublic) return;

        // if (!CanPublicizeMethod(method)) return;

        // Workaround to not publicize a specific method so the game doesn't crash
        if (method.Name == "TryGetScreen") return;

        method.Attributes &= ~MethodAttributes.MemberAccessMask;
        method.Attributes |= MethodAttributes.Public;
    }
}