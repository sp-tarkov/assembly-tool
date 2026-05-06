namespace AssemblyLib.Patching.Tool;

public enum PatchType
{
    /// <summary>
    /// Pre-append a method's instructions to the target
    /// <br/><br/>
    /// Prefixes on void type methods support bool return types to skip the original. return true to skip the original.
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
