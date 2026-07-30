using AssemblyLib.Models;

namespace AssemblyLib.NameFactory.DirectMapRenamers;

public interface IDirectMapRenamer
{
    int Priority { get; }
    bool Enabled { get; }
    ERenamerType Type { get; }
    void Rename(DirectMapModel model);
}
