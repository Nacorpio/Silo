namespace Silo.Styling;

/// <summary>The kind of a <see cref="JsoncScalar"/>.</summary>
public enum JsoncScalarKind
{
    /// <summary>A string, written with double quotes.</summary>
    String,

    /// <summary>A number.</summary>
    Number,

    /// <summary><c>true</c> or <c>false</c>.</summary>
    Boolean,

    /// <summary><c>null</c>.</summary>
    Null,
}

/// <summary>A value in a <see cref="JsoncDocument"/>: a <see cref="JsoncScalar"/> or a <see cref="JsoncObject"/>.</summary>
/// <remarks>
/// A node knows where it starts and ends in the text of the document it belongs to, and nothing else: comments and white
/// space between nodes are not nodes, they are simply the text that no node covers, which is why editing keeps them.
/// </remarks>
public abstract class JsoncValue
{
    /// <summary>Initializes the position of a value.</summary>
    /// <param name="start">The offset of the first character.</param>
    /// <param name="end">The offset after the last character.</param>
    private protected JsoncValue(int start, int end)
    {
        Start = start;
        End = end;
    }

    /// <summary>Gets the offset of the first character of the value in the document text.</summary>
    public int Start { get; }

    /// <summary>Gets the offset after the last character of the value in the document text.</summary>
    public int End { get; }
}

/// <summary>A string, number, boolean or null value.</summary>
public sealed class JsoncScalar : JsoncValue
{
    /// <summary>Creates a scalar.</summary>
    /// <param name="start">The offset of the first character.</param>
    /// <param name="end">The offset after the last character.</param>
    /// <param name="kind">The kind of value.</param>
    /// <param name="text">The text as written, including the quotes of a string.</param>
    /// <param name="value">The value as text: a string without its quotes and escapes, or the written form of the others.</param>
    internal JsoncScalar(int start, int end, JsoncScalarKind kind, string text, string value)
        : base(start, end)
    {
        Kind = kind;
        Text = text;
        Value = value;
    }

    /// <summary>Gets the kind of value.</summary>
    public JsoncScalarKind Kind { get; }

    /// <summary>Gets the text as it is written in the document, including the quotes of a string.</summary>
    public string Text { get; }

    /// <summary>Gets the value as text: a string without its quotes and with escapes resolved, or the written form of the rest.</summary>
    public string Value { get; }
}

/// <summary>An object: <c>{ ... }</c>, holding members in source order, duplicates included.</summary>
public sealed class JsoncObject : JsoncValue
{
    /// <summary>Creates an object.</summary>
    /// <param name="start">The offset of the opening brace.</param>
    /// <param name="end">The offset after the closing brace.</param>
    /// <param name="members">The members in source order.</param>
    internal JsoncObject(int start, int end, IReadOnlyList<JsoncMember> members)
        : base(start, end)
        => Members = members;

    /// <summary>Gets the members in source order. A key that is written twice is two members.</summary>
    public IReadOnlyList<JsoncMember> Members { get; }
}

/// <summary>A <c>"key": value</c> pair of an object.</summary>
public sealed class JsoncMember
{
    /// <summary>Creates a member.</summary>
    /// <param name="keyStart">The offset of the opening quote of the key.</param>
    /// <param name="keyEnd">The offset after the closing quote of the key.</param>
    /// <param name="key">The key with its escapes resolved.</param>
    /// <param name="value">The value.</param>
    /// <param name="comma">The offset of the comma that follows the member, or -1 when there is none.</param>
    internal JsoncMember(int keyStart, int keyEnd, string key, JsoncValue value, int comma)
    {
        KeyStart = keyStart;
        KeyEnd = keyEnd;
        Key = key;
        Value = value;
        Comma = comma;
    }

    /// <summary>Gets the key with its escapes resolved.</summary>
    public string Key { get; }

    /// <summary>Gets the value.</summary>
    public JsoncValue Value { get; }

    /// <summary>Gets the offset of the opening quote of the key.</summary>
    public int KeyStart { get; }

    /// <summary>Gets the offset after the closing quote of the key.</summary>
    public int KeyEnd { get; }

    /// <summary>Gets the offset of the comma after the member, or -1 for the last member when it has no trailing comma.</summary>
    public int Comma { get; }

    /// <summary>Gets the offset of the first character of the member, which is the key's opening quote.</summary>
    public int Start => KeyStart;

    /// <summary>Gets the offset after the member, not counting its comma.</summary>
    public int End => Value.End;
}
