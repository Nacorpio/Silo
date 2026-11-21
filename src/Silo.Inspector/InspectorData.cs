using System.Text.Json;

namespace Silo.Inspector;

/// <summary>One Element in the Inspector's tree, as the diagnostics agent reported it.</summary>
/// <param name="Id">The id that the agent gave the Element.</param>
/// <param name="Type">The Element's type name, such as <c>Button</c>.</param>
/// <param name="Name">The Element's name, or <see langword="null"/>.</param>
/// <param name="Classes">The Classes on the Element.</param>
/// <param name="Children">The child Elements.</param>
public sealed record InspectorNode(
    int Id,
    string Type,
    string? Name,
    IReadOnlyList<string> Classes,
    IReadOnlyList<InspectorNode> Children)
{
    /// <summary>Reads a node from a <c>tree.snapshot</c> result.</summary>
    /// <param name="json">The result, or one of its <c>children</c>.</param>
    /// <returns>The node with all of its descendants.</returns>
    public static InspectorNode Parse(JsonElement json) => new(
        json.GetProperty("id").GetInt32(),
        json.GetProperty("type").GetString()!,
        json.TryGetProperty("name", out var name) ? name.GetString() : null,
        json.GetProperty("classes").EnumerateArray().Select(c => c.GetString()!).ToList(),
        json.GetProperty("children").EnumerateArray().Select(Parse).ToList());

    /// <summary>Gets the text that the tree shows for this node, such as <c>Button#ok.primary</c>.</summary>
    public string Label => $"{Type}{(Name is null ? "" : "#" + Name)}{string.Concat(Classes.Select(c => "." + c))}";
}

/// <summary>One Styled property of the selected Element, with where its value came from.</summary>
/// <param name="Name">The property's name in Stylesheets, such as <c>border-radius</c>.</param>
/// <param name="Value">The current value as text.</param>
/// <param name="Origin">Where the value came from: <c>local</c>, <c>stylesheet</c> or <c>default</c>.</param>
/// <param name="Declared">The text the winning Rule declared, for a Stylesheet origin.</param>
/// <param name="Source">The file, line and column of the winning Rule, such as <c>theme.jsonc:12:5</c>, for a Stylesheet origin.</param>
public sealed record InspectorProperty(string Name, string Value, string Origin, string? Declared, string? Source)
{
    /// <summary>Reads the properties from an <c>element.cascade</c> result.</summary>
    /// <param name="json">The result.</param>
    /// <returns>The properties, in the order the agent listed them.</returns>
    public static IReadOnlyList<InspectorProperty> ParseAll(JsonElement json)
        => json.GetProperty("properties").EnumerateArray().Select(Parse).ToList();

    /// <summary>Reads one property.</summary>
    /// <param name="json">The property's JSON.</param>
    /// <returns>The property.</returns>
    static InspectorProperty Parse(JsonElement json) => new(
        json.GetProperty("name").GetString()!,
        json.GetProperty("value").GetString() ?? "",
        json.GetProperty("origin").GetString()!,
        json.TryGetProperty("declared", out var declared) ? declared.GetString() : null,
        json.TryGetProperty("source", out var source) ? Location(source) : null);

    /// <summary>Formats a source location as <c>file:line:column</c>.</summary>
    /// <param name="source">The <c>source</c> object of a property.</param>
    /// <returns>The location text.</returns>
    static string Location(JsonElement source)
    {
        var file = source.GetProperty("file").GetString();
        var line = source.GetProperty("line").GetInt32();
        var column = source.GetProperty("column").GetInt32();

        return $"{file}:{line}:{column}";
    }

    /// <summary>Gets the text that the property panel shows for this property.</summary>
    public string Label => Origin switch
    {
        "stylesheet" => $"{Name}: {Value}   ({Declared} at {Source})",
        "local" => $"{Name}: {Value}   (local value)",

        _ => $"{Name}: {Value}",
    };
}
