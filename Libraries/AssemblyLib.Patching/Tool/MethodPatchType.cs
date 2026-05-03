namespace AssemblyLib.Patching.Tool;

public enum MethodPatchType
{
    /// <summary>
    /// Pre-append a method's instructions to the target
    /// </summary>
    Prefix,

    /// <summary>
    /// append a methods instructions to the target
    /// </summary>
    Postfix,

    /// <summary>
    /// Replace a method entirely
    /// </summary>
    Replace,
}
