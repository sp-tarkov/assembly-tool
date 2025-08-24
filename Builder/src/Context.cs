using Cake.Common;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;
using Path = System.IO.Path;

namespace SptBuilder;

public class Context : FrostingContext
{
    public string MsBuildConfiguration { get; set; }
    public bool Diagnostic { get; set; } = true;

    public Context(ICakeContext context)
        : base(context)
    {
        // set the working dir to be solution directory
        context.Environment.WorkingDirectory = new DirectoryPath(Path.GetFullPath("../"));
        // Set build configuration to Release if one is not supplied
        MsBuildConfiguration = context.Argument("configuration", "Release");

        if (context.HasArgument("diagnostic"))
            context.Log.Verbosity = Verbosity.Diagnostic;
    }
}
