namespace Silo;

/// <summary>
/// A Button that toggles between on and off each time it is clicked, with a square indicator.
/// </summary>
/// <example>
/// <code language="csharp">
/// var remember = new CheckBox { Text = "Remember me" };
///
/// remember.CheckedChanged += (_, _) => settings.Remember = remember.IsChecked;
/// </code>
/// </example>
public class CheckBox : ToggleButton
{
}
