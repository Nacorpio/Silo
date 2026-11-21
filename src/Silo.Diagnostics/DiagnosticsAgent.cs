using System.Buffers;
using System.Globalization;
using System.Text.Json;
using Silo.Styling;

namespace Silo.Diagnostics;

/// <summary>
/// Answers JSON-RPC 2.0 requests about a live Element tree, which is what the Inspector and other tools talk to.
/// </summary>
/// <remarks>
/// <para>
/// The agent knows nothing about transports: <see cref="Handle"/> takes one request as text and returns one response as
/// text, so a named pipe, a socket or a test can all drive it. Methods in protocol version <see cref="ProtocolVersion"/>:
/// </para>
/// <list type="bullet">
/// <item><description><c>hello</c> returns the protocol version.</description></item>
/// <item><description>
/// <c>tree.snapshot</c> with an optional <c>id</c> returns that Element and its descendants: <c>id</c>, <c>type</c>,
/// <c>name</c>, <c>classes</c>, <c>pseudoStates</c>, <c>bounds</c> for Controls, and <c>children</c>.
/// </description></item>
/// <item><description>
/// <c>element.cascade</c> with an <c>id</c> returns every Styled property of that Element with its current value and where
/// the value came from: <c>origin</c> is <c>local</c>, <c>stylesheet</c> or <c>default</c>, and a Stylesheet origin carries
/// the <c>declared</c> and <c>computed</c> text and the <c>source</c> file, line and column of the winning Rule.
/// </description></item>
/// </list>
/// <para>Element ids are small integers that the agent hands out the first time it sees an Element and keeps for as long as the Element lives.</para>
/// <para>Call it on the UI thread, because it reads the live tree.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var agent = new DiagnosticsAgent(root, () => resolver);
///
/// var reply = agent.Handle("""{ "jsonrpc": "2.0", "id": 1, "method": "tree.snapshot" }""");
/// </code>
/// </example>
public sealed class DiagnosticsAgent(Element root, Func<StyleResolver?> styles)
{
    /// <summary>The version of the protocol that this agent speaks.</summary>
    public const int ProtocolVersion = 1;

    /// <summary>The JSON-RPC code for text that is not JSON.</summary>
    const int ParseError = -32700;

    /// <summary>The JSON-RPC code for JSON that is not a request.</summary>
    const int InvalidRequest = -32600;

    /// <summary>The JSON-RPC code for a method that does not exist.</summary>
    const int MethodNotFound = -32601;

    /// <summary>The JSON-RPC code for missing or wrong parameters.</summary>
    const int InvalidParams = -32602;


    /// <summary>The id that each Element was given.</summary>
    readonly System.Runtime.CompilerServices.ConditionalWeakTable<Element, object> ids = new();

    /// <summary>The Elements by id, held weakly so that the agent does not keep removed Elements alive.</summary>
    readonly Dictionary<int, WeakReference<Element>> byId = [];


    /// <summary>The last id that was handed out.</summary>
    int lastId;

    /// <summary>Handles one request.</summary>
    /// <param name="request">One JSON-RPC 2.0 request.</param>
    /// <returns>The JSON-RPC response, or <see langword="null"/> for a notification, which is a request without an <c>id</c>.</returns>
    public string? Handle(string request)
    {
        ArgumentNullException.ThrowIfNull(request);

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(request);
        }
        catch (JsonException)
        {
            return Error(default, ParseError, "The request is not valid JSON.");
        }

        using (document)
            return HandleDocument(document.RootElement);
    }

    /// <summary>Handles a parsed request.</summary>
    /// <param name="request">The root of the request.</param>
    /// <returns>The response, or <see langword="null"/> for a notification.</returns>
    string? HandleDocument(JsonElement request)
    {
        request.TryGetProperty("id", out var id);

        var notification = id.ValueKind == JsonValueKind.Undefined;

        if (request.ValueKind != JsonValueKind.Object
            || !request.TryGetProperty("method", out var method)
            || method.ValueKind != JsonValueKind.String)
            return Error(id, InvalidRequest, "The request needs a string 'method'.");

        request.TryGetProperty("params", out var parameters);

        var result = Dispatch(method.GetString()!, parameters, out var code, out var message);

        if (notification)
            return null;

        return result is null ? Error(id, code, message!) : Result(id, result);
    }

    /// <summary>Runs one method.</summary>
    /// <param name="method">The method name.</param>
    /// <param name="parameters">The <c>params</c> value, which is undefined when absent.</param>
    /// <param name="code">The JSON-RPC error code when the method failed.</param>
    /// <param name="message">The error message when the method failed.</param>
    /// <returns>The result as JSON text, or <see langword="null"/> when the method failed.</returns>
    string? Dispatch(string method, JsonElement parameters, out int code, out string? message)
    {
        code = 0;
        message = null;

        switch (method)
        {
            case "hello":
                return Write(w => w.WriteNumber("protocol", ProtocolVersion), wrap: true);

            case "tree.snapshot":
            {
                var start = IdParameter(parameters) is { } id ? Find(id) : root;

                if (start is null)
                {
                    code = InvalidParams;
                    message = "There is no Element with that id.";

                    return null;
                }

                return Write(w => WriteNode(w, start));
            }

            case "element.cascade":
            {
                var target = IdParameter(parameters) is { } id ? Find(id) : null;

                if (target is null)
                {
                    code = InvalidParams;
                    message = "The 'id' parameter must name an existing Element.";

                    return null;
                }

                return Write(w => WriteCascade(w, target));
            }

            default:
                code = MethodNotFound;
                message = $"Unknown method '{method}'.";

                return null;
        }
    }

    /// <summary>Reads the <c>id</c> member of the parameters.</summary>
    /// <param name="parameters">The <c>params</c> value.</param>
    /// <returns>The id, or <see langword="null"/> when it is absent or not a number.</returns>
    static int? IdParameter(JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object || !parameters.TryGetProperty("id", out var id))
            return null;

        return id.TryGetInt32(out var value) ? value : null;
    }

    /// <summary>Gets the id of an Element, handing one out the first time.</summary>
    /// <param name="element">The Element.</param>
    /// <returns>Its id.</returns>
    int IdOf(Element element)
    {
        if (ids.TryGetValue(element, out var existing))
            return (int)existing;

        var id = ++lastId;

        ids.Add(element, id);
        byId[id] = new WeakReference<Element>(element);

        return id;
    }

    /// <summary>Finds an Element by id.</summary>
    /// <param name="id">The id.</param>
    /// <returns>The Element, or <see langword="null"/> when the id is unknown or the Element is gone.</returns>
    Element? Find(int id) => byId.TryGetValue(id, out var weak) && weak.TryGetTarget(out var element) ? element : null;

    /// <summary>Writes an Element and its descendants.</summary>
    /// <param name="w">The writer.</param>
    /// <param name="element">The Element.</param>
    void WriteNode(Utf8JsonWriter w, Element element)
    {
        w.WriteStartObject();
        w.WriteNumber("id", IdOf(element));
        w.WriteString("type", element.TypeName);

        if (element.Name is not null)
            w.WriteString("name", element.Name);

        WriteStrings(w, "classes", element.Classes);
        WriteStrings(w, "pseudoStates", element.PseudoStates);

        if (element is Control control)
            WriteBounds(w, control.Bounds);

        w.WriteStartArray("children");

        foreach (var child in element.Children)
            WriteNode(w, child);

        w.WriteEndArray();
        w.WriteEndObject();
    }

    /// <summary>Writes a list of strings.</summary>
    /// <param name="w">The writer.</param>
    /// <param name="name">The member name.</param>
    /// <param name="values">The strings.</param>
    static void WriteStrings(Utf8JsonWriter w, string name, IEnumerable<string> values)
    {
        w.WriteStartArray(name);

        foreach (var value in values)
            w.WriteStringValue(value);

        w.WriteEndArray();
    }

    /// <summary>Writes the bounds of a Control, relative to its parent.</summary>
    /// <param name="w">The writer.</param>
    /// <param name="bounds">The bounds.</param>
    static void WriteBounds(Utf8JsonWriter w, Layout.Rect bounds)
    {
        w.WriteStartObject("bounds");
        w.WriteNumber("x", bounds.X);
        w.WriteNumber("y", bounds.Y);
        w.WriteNumber("width", bounds.Width);
        w.WriteNumber("height", bounds.Height);
        w.WriteEndObject();
    }

    /// <summary>Writes every Styled property of an Element with its value and origin.</summary>
    /// <param name="w">The writer.</param>
    /// <param name="element">The Element.</param>
    void WriteCascade(Utf8JsonWriter w, Element element)
    {
        var resolver = styles();
        var cascaded = resolver?.Cascade(element);
        var tokens = resolver?.ResolveTokens(element);

        w.WriteStartObject();
        w.WriteNumber("id", IdOf(element));
        w.WriteString("type", element.TypeName);
        w.WriteStartArray("properties");

        foreach (var property in StyledProperty.GetProperties(element.GetType()))
        {
            w.WriteStartObject();
            w.WriteString("name", property.Name);
            w.WriteString("value", Convert.ToString(element.GetValueObject(property), CultureInfo.InvariantCulture));

            if (element.HasLocalValue(property))
                w.WriteString("origin", "local");
            else if (cascaded is not null && cascaded.TryGetValue(property.Name, out var winner))
                WriteStylesheetOrigin(w, winner, tokens!);
            else
                w.WriteString("origin", "default");

            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteEndObject();
    }

    /// <summary>Writes the Stylesheet origin of a value.</summary>
    /// <param name="w">The writer.</param>
    /// <param name="winner">The winning declaration.</param>
    /// <param name="tokens">The Tokens visible at the Element.</param>
    static void WriteStylesheetOrigin(
        Utf8JsonWriter w,
        CascadedValue winner,
        IReadOnlyDictionary<string, string> tokens)
    {
        w.WriteString("origin", "stylesheet");
        w.WriteString("declared", winner.Value);

        try
        {
            w.WriteString("computed", StyleResolver.Compute(winner.Value, tokens));
        }
        catch (StylesheetException)
        {
            // The Element could not use this value either, so there is no computed text to show.
        }

        w.WriteStartObject("source");
        w.WriteString("file", winner.Source.Span.File);
        w.WriteNumber("line", winner.Source.Span.Line);
        w.WriteNumber("column", winner.Source.Span.Column);
        w.WriteEndObject();
    }

    /// <summary>Writes a JSON value into a string.</summary>
    /// <param name="write">Writes the value.</param>
    /// <param name="wrap">Whether <paramref name="write"/> writes object members, so that the object is started and ended here.</param>
    /// <returns>The JSON text.</returns>
    static string Write(Action<Utf8JsonWriter> write, bool wrap = false)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var w = new Utf8JsonWriter(buffer))
        {
            if (wrap)
                w.WriteStartObject();

            write(w);

            if (wrap)
                w.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>Builds a success response.</summary>
    /// <param name="id">The request id.</param>
    /// <param name="result">The result as JSON text.</param>
    /// <returns>The response.</returns>
    static string Result(JsonElement id, string result) =>
        $$"""{"jsonrpc":"2.0","id":{{id.GetRawText()}},"result":{{result}}}""";

    /// <summary>Builds an error response.</summary>
    /// <param name="id">The request id, which is undefined when it could not be read.</param>
    /// <param name="code">The JSON-RPC error code.</param>
    /// <param name="message">The message.</param>
    /// <returns>The response.</returns>
    static string Error(JsonElement id, int code, string message)
    {
        var idText = id.ValueKind == JsonValueKind.Undefined ? "null" : id.GetRawText();
        var messageText = Write(w => w.WriteStringValue(message));

        return $$$"""{"jsonrpc":"2.0","id":{{{idText}}},"error":{"code":{{{code}}},"message":{{{messageText}}}}}""";
    }
}
