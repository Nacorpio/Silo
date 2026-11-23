namespace Silo.Studio;

/// <summary>What the category list shows about a category of Controls.</summary>
/// <param name="Glyph">The name of the icon.</param>
/// <param name="Description">A short description of the Controls in it.</param>
/// <param name="Count">How many Controls it holds.</param>
public sealed record CategoryInfo(string Glyph, string Description, int Count);
