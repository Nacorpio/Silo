using System.Globalization;
using Silo.Rendering.Skia;
using SkiaSharp;
using UnitsNet;
using Color = Silo.Styling.Values.Color;

namespace Silo.Studio;

public static partial class ControlCatalog
{
    /// <summary>Gets the entries of the Menus and dialogs category.</summary>
    static IEnumerable<CatalogEntry> MenusAndDialogs() =>
    [
        new("Tooltip", "Menus and dialogs", CreateTooltips),
        new("Toolbar", "Menus and dialogs", CreateToolbar),
        new("MenuBar", "Menus and dialogs", CreateMenuBar),
        new("ContextMenu", "Menus and dialogs", CreateContextMenu),
        new("Dialog", "Menus and dialogs", CreateDialog),
        new("MessageBox", "Menus and dialogs", CreateMessageBox),
    ];

    /// <summary>Creates a ContextMenu that is not shown in a window, with a highlighted entry, a separator and a submenu arrow.</summary>
    /// <returns>The ContextMenu.</returns>
    static ContextMenu CreateContextMenu()
    {
        var entries = new List<MenuItem>
        {
            new("&Copy") { ShortcutText = "Ctrl+C", Glyph = "plus" },
            new("&Paste") { ShortcutText = "Ctrl+V", IsEnabled = false, Glyph = "list" },
            MenuItem.Separator,
            new("&Word wrap") { IsChecked = true },
            new("&Small") { IsChecked = false, GroupName = "size" },
            new("&Large") { IsChecked = true, GroupName = "size" },
            MenuItem.Separator,
            new("&Recent") { Glyph = "home", Children = [new MenuItem("a.txt")] },
        };

        var menu = new ContextMenu { Entries = entries, SelectedIndex = 0 };

        menu.SetValue(Control.FlexShrinkProperty, 0);

        return menu;
    }

    /// <summary>Creates a MenuBar with two menus.</summary>
    /// <returns>The MenuBar.</returns>
    static MenuBar CreateMenuBar()
    {
        var bar = new MenuBar();

        bar.AddMenu("&File", [new MenuItem("Open"), new MenuItem("Exit")]);
        bar.AddMenu("&Help", [new MenuItem("About")]);

        return bar;
    }

    /// <summary>Creates a MessageBox dialog, wide enough for its two lines.</summary>
    /// <returns>The Dialog.</returns>
    static Dialog CreateMessageBox()
    {
        var dialog = MessageBox.Create("Delete the file?\nThis cannot be undone.", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        dialog.SetValue(Control.WidthProperty, new Silo.Styling.Values.Length(260, Silo.Styling.Values.LengthUnit.Px));

        return dialog;
    }

    /// <summary>Creates a Dialog that is not shown in a window, so that its title, content and buttons show in the matrix.</summary>
    /// <returns>The Dialog.</returns>
    static Dialog CreateDialog()
    {
        var dialog = new Dialog { Title = "Dialog", Content = new TextBlock { Text = "Are you sure?" } };

        dialog.AddButton("Cancel");
        dialog.AddButton("OK");

        return dialog;
    }

    /// <summary>Creates a column of a plain Tooltip and one with an icon, a header, a shortcut and a description.</summary>
    static Box CreateTooltips()
    {
        var column = new Box();

        column.AddClass("tooltips");

        column.Children.Add(new Tooltip { Text = "Save the document", Shortcut = "Ctrl+S" });
        column.Children.Add(new Tooltip
        {
            Glyph = "info",
            Header = "Save",
            Shortcut = "Ctrl+S",
            Text = "Write the document to disk",
            Description = "Saves to the file the document was opened from. Unsaved changes are kept in a backup.",
        });

        return column;
    }

    /// <summary>Creates a Toolbar with two buttons, a divider and a third button.</summary>
    /// <returns>The Toolbar.</returns>
    static Toolbar CreateToolbar()
    {
        var toolbar = new Toolbar();

        toolbar.AddButton("Cut");
        toolbar.AddButton("Copy");
        toolbar.AddSeparator();
        toolbar.AddButton("Paste");

        return toolbar;
    }
}
