using dnlib.DotNet;

namespace ReCodeItLib.Dumper;

public class TestAssResolver : AssemblyResolver
{
    // TODO: [CWX] tried overriding a few things, even passing back all assemblies from managed folder
    public TestAssResolver(string path, ModuleContext context = null) : base(context)
    {
        ManagedPath = path;
    }

    public string? ManagedPath { get; set; }

    protected override IEnumerable<string> PreFindAssemblies(IAssembly assembly, ModuleDef sourceModule, bool matchExactly)
    {
        // get all files in dir
        // return them as list of strings
        Console.WriteLine("FUCKING HELL");

        var array = Directory.GetFiles(ManagedPath, "*.dll");
        var array2 = base.PreFindAssemblies(assembly, sourceModule, matchExactly).ToArray();
        Array.Copy(array2, array, array2.Length);

        return array;
    }
}