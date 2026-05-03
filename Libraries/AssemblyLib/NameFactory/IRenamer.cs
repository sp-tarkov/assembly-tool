using AssemblyLib.Models;

namespace AssemblyLib.NameFactory;

public interface IRenamer
{
    int Priority { get; }
    bool Enabled { get; }
    ERenamerType Type { get; }
    void Rename(DirectMapModel model);
}
