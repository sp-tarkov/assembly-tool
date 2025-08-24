﻿using System;
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
        BuildAssemblyTool(context);
    }

    private void BuildDe4dot(Context context)
    {
        context.Log.Information("Building De4dot");
        context.DotNetBuild(
            Path.Combine(context.Environment.WorkingDirectory.FullPath, "de4dot\\de4dot-x64\\de4dot-x64.csproj"),
            new DotNetBuildSettings { Configuration = context.MsBuildConfiguration }
        );

        context.DotNetBuild(
            Path.Combine(
                context.Environment.WorkingDirectory.FullPath,
                "de4dot\\AssemblyServer-x64\\AssemblyServer-x64.csproj"
            ),
            new DotNetBuildSettings { Configuration = context.MsBuildConfiguration }
        );
        context.Log.Information("Finished Building De4dot");
    }

    private void BuildAssemblyTool(Context context)
    {
        context.Log.Information("Building Assembly Tool");
        context.DotNetBuild(
            Path.Combine(context.Environment.WorkingDirectory.FullPath, "AssemblyTool\\AssemblyTool.csproj"),
            new DotNetBuildSettings { Configuration = context.MsBuildConfiguration }
        );
        context.Log.Information("Finished Building Assembly Tool");
    }
}
