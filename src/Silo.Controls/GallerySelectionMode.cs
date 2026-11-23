namespace Silo;

/// <summary>How many items of a <see cref="GalleryView"/> the user can select.</summary>
public enum GallerySelectionMode
{
    /// <summary>Items cannot be selected. They can still be activated.</summary>
    None,

    /// <summary>One item at a time.</summary>
    Single,

    /// <summary>Any number: Ctrl-click toggles an item and Shift-click selects a range.</summary>
    Multiple,
}
