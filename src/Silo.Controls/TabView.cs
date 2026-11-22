namespace Silo;

/// <summary>Shows one of several pieces of content at a time, chosen by clicking its header in a row above.</summary>
/// <remarks>
/// <para>
/// A TabView stacks its <see cref="Strip"/> above the content of the selected tab. Only the selected content is part of the
/// tree, so the others take no space and are not drawn. The first tab that is added is selected.
/// </para>
/// <para>
/// Style the headers with the <c>TabHeader</c> type, and the selected one with <c>TabHeader:selected</c>. Styles must be
/// applied again after the selection changes.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var tabs = new TabView();
///
/// tabs.AddTab("General", new TextBlock { Text = "General settings" });
/// tabs.AddTab("Advanced", new TextBlock { Text = "Advanced settings" });
///
/// tabs.SelectionChanged += (_, _) => settings.LastTab = tabs.SelectedIndex;
/// tabs.SelectedIndex = 1;
/// </code>
/// </example>
public class TabView : Control
{
    /// <summary>The row of headers.</summary>
    readonly TabStrip strip = new();

    /// <summary>The headers, in the order of the tabs.</summary>
    readonly List<TabHeader> headers = [];

    /// <summary>The contents, in the order of the tabs.</summary>
    readonly List<Control> contents = [];

    /// <summary>The index of the selected tab, or -1 while there is none.</summary>
    int selectedIndex = -1;

    /// <summary>Creates a TabView without tabs.</summary>
    public TabView() => Children.Add(strip);

    /// <summary>Raised after <see cref="SelectedIndex"/> changed.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Gets the row of headers, so that code can add a Class to it.</summary>
    public TabStrip Strip => strip;

    /// <summary>Gets the number of tabs.</summary>
    public int Count => contents.Count;

    /// <summary>Gets or sets the index of the selected tab, or -1 while there is none.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The index is not -1 or the index of a tab.</exception>
    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, -1);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(value, contents.Count);

            if (value == selectedIndex)
                return;

            if (selectedIndex >= 0)
            {
                headers[selectedIndex].IsSelected = false;

                Children.Remove(contents[selectedIndex]);
            }

            selectedIndex = value;

            if (value >= 0)
            {
                headers[value].IsSelected = true;

                Children.Add(contents[value]);
            }

            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Gets the content of the selected tab, or <see langword="null"/> while there is none.</summary>
    public Control? SelectedContent => selectedIndex < 0 ? null : contents[selectedIndex];

    /// <summary>Adds a tab at the end. The first tab that is added is selected.</summary>
    /// <param name="header">The title in the header.</param>
    /// <param name="content">What is shown while the tab is selected.</param>
    /// <returns>The index of the new tab.</returns>
    public int AddTab(string header, Control content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var tab = new TabHeader { Text = header };
        var index = contents.Count;

        tab.Click += (_, _) => SelectedIndex = headers.IndexOf(tab);

        headers.Add(tab);
        contents.Add(content);
        strip.Children.Add(tab);

        if (selectedIndex < 0)
            SelectedIndex = index;

        return index;
    }

    /// <summary>Gets the header of a tab.</summary>
    /// <param name="index">The index of the tab.</param>
    /// <returns>The header.</returns>
    public TabHeader GetHeader(int index) => headers[index];

    /// <summary>Gets the content of a tab.</summary>
    /// <param name="index">The index of the tab.</param>
    /// <returns>The content.</returns>
    public Control GetContent(int index) => contents[index];
}
