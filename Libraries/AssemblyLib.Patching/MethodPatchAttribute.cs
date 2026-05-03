using System;

namespace AssemblyLib.Patching;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class MethodPatchAttribute(Type targetType, string methodName, MethodPatchType patchType) : Attribute
{
    public Type TargetType => targetType;
    public string MethodName => methodName;
    public MethodPatchType PatchType => patchType;
}
