using System;

namespace AssemblyLib.Patching.Tool;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class MethodPatchAttribute(Type targetType, string methodName, MethodPatchType patchType) : Attribute
{
    public Type TargetType => targetType;
    public string MethodName => methodName;
    public MethodPatchType PatchType => patchType;
}
