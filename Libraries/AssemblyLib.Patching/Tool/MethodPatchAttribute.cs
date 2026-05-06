using System;

namespace AssemblyLib.Patching.Tool;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class MethodPatchAttribute : Attribute
{
    public MethodPatchAttribute(
        Type targetType,
        string methodName,
        MethodPatchType patchType,
        params Type[] methodTypeParams
    )
    {
        TargetType = targetType;
        MethodName = methodName;
        PatchType = patchType;
        TargetMethodParameterTypes = methodTypeParams;
        TargetKind = GetTargetKind(methodName);
    }

    public MethodPatchAttribute(Type targetType, MethodPatchType patchType, params Type[] constructorTypeParams)
        : this(targetType, MethodPatchTargetKind.Constructor, patchType, constructorTypeParams) { }

    public MethodPatchAttribute(
        Type targetType,
        MethodPatchTargetKind targetKind,
        MethodPatchType patchType,
        params Type[] targetTypeParams
    )
    {
        if (targetKind == MethodPatchTargetKind.Method)
        {
            throw new ArgumentException(
                "Method targets must specify a method name. Use the overload that accepts `methodName`.",
                nameof(targetKind)
            );
        }

        if (targetKind == MethodPatchTargetKind.StaticConstructor && targetTypeParams.Length > 0)
        {
            throw new ArgumentException("Static constructors cannot have parameters.", nameof(targetTypeParams));
        }

        TargetType = targetType;
        MethodName = targetKind == MethodPatchTargetKind.StaticConstructor ? ".cctor" : ".ctor";
        PatchType = patchType;
        TargetMethodParameterTypes = targetTypeParams;
        TargetKind = targetKind;
    }

    public Type TargetType { get; }
    public string MethodName { get; }
    public MethodPatchType PatchType { get; }
    public Type[] TargetMethodParameterTypes { get; }
    public MethodPatchTargetKind TargetKind { get; }

    private static MethodPatchTargetKind GetTargetKind(string methodName) =>
        methodName switch
        {
            ".ctor" => MethodPatchTargetKind.Constructor,
            ".cctor" => MethodPatchTargetKind.StaticConstructor,
            _ => MethodPatchTargetKind.Method,
        };
}
