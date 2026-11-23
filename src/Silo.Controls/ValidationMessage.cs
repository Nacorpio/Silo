namespace Silo;

/// <summary>A line of text that tells the user what is wrong with an input, styled as an error.</summary>
/// <remarks>
/// A ValidationMessage is a <see cref="TextBlock"/> with its own type name, so the theme can give it the error color. With no
/// text it measures nothing. A <see cref="FormField"/> shows one under its input.
/// </remarks>
/// <example>
/// <code language="csharp">
/// var message = new ValidationMessage { Text = "Enter a number" };
/// </code>
/// </example>
public class ValidationMessage : TextBlock
{
}
