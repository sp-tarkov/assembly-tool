namespace AssemblyLib.AttributeFactory;

public interface IAttributeBuilder
{
    public bool Enabled { get; }
    public void Build();
}
