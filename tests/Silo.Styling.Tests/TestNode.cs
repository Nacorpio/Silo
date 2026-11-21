using Silo.Styling;

public sealed class TestNode(
    string type,
    string? name = null,
    string[]? classes = null,
    string[]? states = null,
    TestNode? parent = null) : IStyleNode
{
    public string TypeName => type;
    public string? Name => name;
    public IReadOnlyCollection<string> Classes => classes ?? [];
    public IReadOnlyCollection<string> PseudoStates => states ?? [];
    public IStyleNode? Parent => parent;
}
