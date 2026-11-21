namespace Silo.Styling;

/// <summary>What the styling engine needs to know about an Element to match Selectors.</summary>
public interface IStyleNode
{
    string TypeName { get; }
    string? Name { get; }
    IReadOnlyCollection<string> Classes { get; }
    IReadOnlyCollection<string> PseudoStates { get; }
    IStyleNode? Parent { get; }
}
