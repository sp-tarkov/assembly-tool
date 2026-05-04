using System;

namespace AssemblyLib.Patching.Tool;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class MethodPatchAttribute(
    Type targetType,
    string methodName,
    MethodPatchType patchType,
    params Type[] methodTypeParams
) : Attribute
{
    public Type TargetType => targetType;
    public string MethodName => methodName;
    public MethodPatchType PatchType => patchType;
    public Type[] TargetMethodParameterTypes => methodTypeParams;
}
