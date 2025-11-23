using AssemblyLib.Models;

namespace AssemblyLib.DirectMapper.Renamers;

public interface IRenamer
{
    int Priority { get; }
    ERenamerType Type { get; }
    void Rename(DirectMapModel model);
}
