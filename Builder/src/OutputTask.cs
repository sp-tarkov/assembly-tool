using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cake.Common.IO;
using Cake.Core;
using Cake.Frosting;

namespace SptBuilder;

[TaskName("OutputTask")]
[IsDependentOn(typeof(BuildTask))]
public class OutputTask : FrostingTask<Context>
{
    public override void Run(Context context)
    {
        var buildPath = Path.Combine(context.Environment.WorkingDirectory.FullPath, "Build");
        if (!Directory.Exists(buildPath))
        {
            Directory.CreateDirectory(buildPath);
        }

        Directory.CreateDirectory(Path.Combine(buildPath, "Data"));
        Directory.CreateDirectory(Path.Combine(buildPath, "de4dot"));
        Directory.CreateDirectory(Path.Combine(buildPath, "DUMPDATA"));
        CopyDataFiles(context);
        CopyDe4dotFiles(context);
        CopyDumpFiles(context);
        CopyOtherFiles(context);
    }

    private void CopyDataFiles(Context context)
    {
        var listOfFiles = Directory
            .GetFiles(
                Path.Combine(context.Environment.WorkingDirectory.FullPath, "Assets/Templates")
            )
            .ToList();

        listOfFiles.Add(
            Path.Combine(context.Environment.WorkingDirectory.FullPath, "Assets/mappings.jsonc")
        );

        listOfFiles.Add(
            Path.Combine(context.Environment.WorkingDirectory.FullPath, "Assets/hdiffz.exe")
        );

        context.CopyFiles(
            listOfFiles,
            Path.Combine(context.Environment.WorkingDirectory.FullPath, "Build", "Data")
        );
    }

    private void CopyDe4dotFiles(Context context)
    {
        var listOfFiles = Directory
            .GetFiles(
                Path.Combine(
                    context.Environment.WorkingDirectory.FullPath,
                    "de4dot",
                    context.MsBuildConfiguration,
                    "net48"
                )
            )
            .ToList();

        if (context.MsBuildConfiguration == "Release")
        {
            listOfFiles.RemoveAll(m => m.Contains(".pdb"));
        }

        context.CopyFiles(
            listOfFiles,
            Path.Combine(context.Environment.WorkingDirectory.FullPath, "Build", "de4dot")
        );
    }

    private void CopyDumpFiles(Context context)
    {
        var listOfFiles = Directory.GetFiles(
            Path.Combine(context.Environment.WorkingDirectory.FullPath, "DumpLib/DUMPDATA")
        );

        context.CopyFiles(
            listOfFiles,
            Path.Combine(context.Environment.WorkingDirectory.FullPath, "Build", "DUMPDATA")
        );
    }

    private void CopyOtherFiles(Context context)
    {
        var listOfFiles = Directory
            .GetFiles(
                Path.Combine(
                    context.Environment.WorkingDirectory.FullPath,
                    "AssemblyTool\\bin",
                    context.MsBuildConfiguration,
                    "net9.0"
                )
            )
            .ToList();

        if (context.MsBuildConfiguration == "Release")
        {
            //listOfFiles.RemoveAll(m => m.Contains(".pdb"));
        }

        context.CopyFiles(
            listOfFiles,
            Path.Combine(context.Environment.WorkingDirectory.FullPath, "Build")
        );
    }
}
