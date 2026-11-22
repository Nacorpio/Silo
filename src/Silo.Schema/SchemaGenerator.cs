using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using PropertyMap = System.Collections.Generic.SortedDictionary<string, (Silo.StyledProperty Property, System.Collections.Generic.SortedSet<string> Owners)>;

namespace Silo.Schema;

/// <summary>
/// Generates the JSON Schema of the Stylesheet format from the Styled properties that the Controls register, so editors
/// can complete and check property names and keyword values in a <c>.jsonc</c> theme.
/// </summary>
/// <remarks>
/// <para>
/// The schema has a rule body that lists every known property, accepts Tokens (keys that start with <c>--</c>) and treats
/// any other key whose value is an object as a nested Rule. Selectors are the keys of <c>rules</c> and are not described,
/// since JSON Schema cannot express them.
/// </para>
/// <para>
/// A property whose value is a keyword, such as <c>flex-direction</c>, lists the keywords that its parser accepts. Every
/// other value is a string, number or boolean, because a value can also be a <c>var(--token)</c> reference, and the
/// description tells what the property expects.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// File.WriteAllText("silo-stylesheet.schema.json", SchemaGenerator.Generate());
/// </code>
/// </example>
public static class SchemaGenerator
{
    /// <summary>The keywords that the generator tries on a property whose value is a keyword.</summary>
    static readonly string[] Keywords =
    [
        "row", "column", "start", "end", "flex-start", "flex-end", "center", "stretch", "space-between", "space-around",
        "space-evenly", "left", "right", "normal", "bold", "baseline", "auto",
    ];

    /// <summary>The Control types in <c>Silo.Controls</c> and <see cref="Control"/> itself, with their static state set up.</summary>
    /// <returns>The types in name order.</returns>
    static List<Type> ControlTypes()
    {
        var types = typeof(Control).Assembly.GetTypes()
            .Where(t => typeof(Control).IsAssignableFrom(t) && !t.IsGenericTypeDefinition)
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

        foreach (var type in types)
            RuntimeHelpers.RunClassConstructor(type.TypeHandle);

        return types;
    }

    /// <summary>Generates the schema.</summary>
    /// <returns>The JSON text of the schema, indented, with the properties in name order so the output is stable.</returns>
    public static string Generate()
    {
        var properties = new PropertyMap(StringComparer.Ordinal);

        foreach (var type in ControlTypes())
            foreach (var property in StyledProperty.GetProperties(type).Where(p => p.OwnerType == type))
            {
                if (!properties.TryGetValue(property.Name, out var entry))
                    properties[property.Name] = entry = (property, []);

                entry.Owners.Add(type.Name);
            }

        using var stream = new MemoryStream();

        using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            Write(json, properties);
        }

        return Encoding.UTF8.GetString(stream.ToArray()).ReplaceLineEndings("\n") + "\n";
    }

    /// <summary>Writes the whole schema document.</summary>
    /// <param name="json">The writer.</param>
    /// <param name="properties">The known properties and the types that declare them.</param>
    static void Write(Utf8JsonWriter json, PropertyMap properties)
    {
        json.WriteStartObject();
        json.WriteString("$schema", "https://json-schema.org/draft/2020-12/schema");
        json.WriteString("title", "Silo Stylesheet");
        json.WriteString("description", "A Silo Stylesheet in JSONC: Tokens and Rules written like CSS.");
        json.WriteString("type", "object");
        json.WriteBoolean("additionalProperties", false);

        json.WriteStartObject("properties");

        json.WriteStartObject("$schema");
        json.WriteString("type", "string");
        json.WriteEndObject();

        json.WriteStartObject("tokens");
        json.WriteString("description", "Tokens, like CSS custom properties. Use them as var(--name).");
        json.WriteString("type", "object");
        json.WriteBoolean("additionalProperties", false);

        json.WriteStartObject("patternProperties");
        json.WriteStartObject("^--");
        json.WriteString("$ref", "#/$defs/scalar");
        json.WriteEndObject();
        json.WriteEndObject();

        json.WriteEndObject();

        json.WriteStartObject("rules");
        json.WriteString("description", "Rules by Selector, such as \"Button.primary\" or \"#save:hover\".");
        json.WriteString("type", "object");

        json.WriteStartObject("additionalProperties");
        json.WriteString("$ref", "#/$defs/rule");
        json.WriteEndObject();

        json.WriteEndObject();

        json.WriteEndObject();

        json.WriteStartObject("$defs");

        json.WriteStartObject("scalar");
        json.WriteStartArray("type");
        json.WriteStringValue("string");
        json.WriteStringValue("number");
        json.WriteStringValue("boolean");
        json.WriteEndArray();
        json.WriteEndObject();

        json.WriteStartObject("rule");
        json.WriteString("type", "object");

        json.WriteStartObject("properties");

        foreach (var (name, (property, owners)) in properties)
            WriteProperty(json, name, property, owners);

        json.WriteEndObject();

        json.WriteStartObject("patternProperties");
        json.WriteStartObject("^--");
        json.WriteString("$ref", "#/$defs/scalar");
        json.WriteEndObject();
        json.WriteEndObject();

        json.WriteStartObject("additionalProperties");
        json.WriteString("description", "A nested Rule. A key with & has the parent Selector put in its place.");
        json.WriteString("$ref", "#/$defs/rule");
        json.WriteEndObject();

        json.WriteEndObject();

        json.WriteEndObject();

        json.WriteEndObject();
    }

    /// <summary>Writes the schema of one property.</summary>
    /// <param name="json">The writer.</param>
    /// <param name="name">The property name.</param>
    /// <param name="property">The first property that has the name.</param>
    /// <param name="owners">The names of the Control types that declare it.</param>
    static void WriteProperty(Utf8JsonWriter json, string name, StyledProperty property, SortedSet<string> owners)
    {
        var keywords = KeywordsOf(property);

        json.WriteStartObject(name);
        json.WriteString("description", $"{Describe(property)} Declared by {string.Join(", ", owners)}.");

        if (keywords.Count == 0)
            json.WriteString("$ref", "#/$defs/scalar");
        else
        {
            json.WriteStartArray("anyOf");

            json.WriteStartObject();
            json.WriteStartArray("enum");

            foreach (var keyword in keywords)
                json.WriteStringValue(keyword);

            json.WriteEndArray();
            json.WriteEndObject();

            json.WriteStartObject();
            json.WriteString("pattern", "^\\s*var\\(");
            json.WriteEndObject();

            json.WriteEndArray();
        }

        json.WriteEndObject();
    }

    /// <summary>Finds the keywords that a keyword property accepts.</summary>
    /// <param name="property">The property.</param>
    /// <returns>The accepted keywords, or an empty list when the value is not a keyword.</returns>
    static List<string> KeywordsOf(StyledProperty property)
    {
        var type = Nullable.GetUnderlyingType(property.ValueType) ?? property.ValueType;

        return type.IsEnum ? Keywords.Where(property.Accepts).ToList() : [];
    }

    /// <summary>Tells what kind of value a property expects.</summary>
    /// <param name="property">The property.</param>
    /// <returns>A sentence.</returns>
    static string Describe(StyledProperty property)
    {
        var type = Nullable.GetUnderlyingType(property.ValueType) ?? property.ValueType;

        return type.Name switch
        {
            "Color" => "A color such as #3b82f6 or oklch(0.6 0.15 250).",
            "Length" => "A length such as 12px or 50%.",
            "Thickness" => "One to four lengths, like CSS padding.",
            "Double" => "A number.",
            "String" => "Text.",
            "Boolean" => "A flag written as a keyword or number, such as normal, bold or 700 for font-weight.",

            _ when type.IsEnum => "One of the keywords.",

            _ => $"A {type.Name}.",
        };
    }
}
