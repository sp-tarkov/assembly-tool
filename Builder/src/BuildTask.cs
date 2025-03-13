using System;
using System.IO;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Build;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace SptBuilder;

[TaskName("BuildTask")]
[IsDependentOn(typeof(CleanTask))]
public class BuildTask : FrostingTask<Context>
{
    public override void Run(Context context)
    {
        BuildDe4dot(context);
        BuildReCodeIt(context);
    }

    private void BuildDe4dot(Context context)
    {
        context.Log.Information("Building De4dot");
        context.DotNetBuild(Path.Combine(context.Environment.WorkingDirectory.FullPath, "de4dot\\de4dot-x64\\de4dot-x64.csproj"),
            new DotNetBuildSettings
            {
                Configuration = context.MsBuildConfiguration,
            }
        );
        context.Log.Information("Finished Building De4dot");
    }

    private void BuildReCodeIt(Context context)
    {
        context.Log.Information("Building ReCodeIt");
        context.DotNetBuild(Path.Combine(context.Environment.WorkingDirectory.FullPath, "ReCodeItCli\\ReCodeItCli.csproj"),
            new DotNetBuildSettings
            {
                Configuration = context.MsBuildConfiguration,
            }
        );
        context.Log.Information("Finished Building ReCodeIt");
    }
}