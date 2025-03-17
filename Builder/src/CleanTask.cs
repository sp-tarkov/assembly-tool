using System.IO;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;
using Path = System.IO.Path;

namespace SptBuilder;

[TaskName("CleanTask")]
public class CleanTask : FrostingTask<Context>
{
    public override void Run(Context context)
    {
        CleanBuildFolders(context);
        CleanDe4dotFolders(context);
        //CleanObjFolders(context);
        CleanBinFolders(context);
    }

    private void CleanBuildFolders(Context context)
    {
        context.Log.Information("Cleaning Build Folders");
        context.CleanDirectory(Path.Combine(context.Environment.WorkingDirectory.FullPath, "Build"));
    }

    private void CleanObjFolders(Context context)
    {
        context.Log.Information("Cleaning obj Folders");
        // we do these per project folder to not try to delete the builder bin/obj
        context.CleanDirectories(new GlobPattern("de4dot/**/obj"));
        context.CleanDirectories(new GlobPattern("DumpLib/**/obj"));
        context.CleanDirectories(new GlobPattern("Assembly*/**/obj"));
    }

    private void CleanBinFolders(Context context)
    {
        context.Log.Information("Cleaning bin Folders");
        context.CleanDirectories(new GlobPattern("DumpLib/**/bin"));
        context.CleanDirectories(new GlobPattern("Assembly*/**/bin"));
    }

    private void CleanDe4dotFolders(Context context)
    {
        context.Log.Information("Cleaning De4dot Folders");
        context.CleanDirectory(Path.Combine(context.Environment.WorkingDirectory.FullPath, "de4dot", context.MsBuildConfiguration));
    }
}