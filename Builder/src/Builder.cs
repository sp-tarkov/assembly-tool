using System.Threading.Tasks;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace SptBuilder;

public static class Builder
{
    public static int Main(string[] args)
    {
        return new CakeHost()
            .UseContext<Context>()
            .Run(args);
    }
}

[TaskName("Default")]
[IsDependentOn(typeof(OutputTask))]
public class DefaultTask : FrostingTask
{
    // Docs dont say why this is needed
    // removing it fails Cake looking for a default task
}