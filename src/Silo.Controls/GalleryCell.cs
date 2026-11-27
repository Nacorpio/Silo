namespace Silo;

/// <summary>The Control that holds an item's visual in a <see cref="GalleryView"/> and carries its hover and <c>selected</c> Pseudo-states, which a stylesheet paints through <c>GalleryCell:selected</c>.</summary>
[PseudoStates("selected")]
public sealed class GalleryCell : Control
{
    /// <summary>Runs before the first cell is created, so that the Styled properties are registered in time.</summary>
    static GalleryCell()
    {
    }

    /// <summary>Creates a cell around a visual.</summary>
    /// <param name="visual">What the item looks like.</param>
    internal GalleryCell(Control visual) => Children.Add(visual);

    /// <summary>Sets the selection state.</summary>
    /// <param name="isSelected">Whether the item is selected.</param>
    /// <param name="isCurrent">Whether the keyboard is on the item.</param>
    internal void Update(bool isSelected, bool isCurrent)
    {
        SetPseudoState("selected", isSelected);

        _ = isCurrent ? AddClass("current") : RemoveClass("current");
    }
}
