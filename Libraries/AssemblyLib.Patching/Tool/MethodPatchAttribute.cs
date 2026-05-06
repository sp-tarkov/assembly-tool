using System;

namespace AssemblyLib.Patching.Tool;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class MethodPatchAttribute : Attribute
{
    public MethodPatchAttribute(Type targetType, string methodName, PatchType patchType, params Type[] methodTypeParams)
    {
        TargetType = targetType;
        MethodName = methodName;
        PatchType = patchType;
        TargetMethodParameterTypes = methodTypeParams;
        TargetKind = GetTargetKind(methodName);
    }

    public MethodPatchAttribute(Type targetType, PatchType patchType, params Type[] constructorTypeParams)
        : this(targetType, PatchTargetKind.Constructor, patchType, constructorTypeParams) { }

    public MethodPatchAttribute(
        Type targetType,
        PatchTargetKind targetKind,
        PatchType patchType,
        params Type[] targetTypeParams
    )
    {
        if (targetKind == PatchTargetKind.Method)
        {
            throw new ArgumentException(
                "Method targets must specify a method name. Use the overload that accepts `methodName`.",
                nameof(targetKind)
            );
        }

        if (targetKind == PatchTargetKind.StaticConstructor && targetTypeParams.Length > 0)
        {
            throw new ArgumentException("Static constructors cannot have parameters.", nameof(targetTypeParams));
        }

        TargetType = targetType;
        MethodName = targetKind == PatchTargetKind.StaticConstructor ? ".cctor" : ".ctor";
        PatchType = patchType;
        TargetMethodParameterTypes = targetTypeParams;
        TargetKind = targetKind;
    }

    public Type TargetType { get; }
    public string MethodName { get; }
    public PatchType PatchType { get; }
    public Type[] TargetMethodParameterTypes { get; }
    public PatchTargetKind TargetKind { get; }

    private static PatchTargetKind GetTargetKind(string methodName) =>
        methodName switch
        {
            ".ctor" => PatchTargetKind.Constructor,
            ".cctor" => PatchTargetKind.StaticConstructor,
            _ => PatchTargetKind.Method,
        };
}
