namespace Silo;

/// <summary>Which button closed a <see cref="MessageBox"/>.</summary>
public enum DialogResult
{
    /// <summary>The box could not be shown, for example because the anchor is not in a window.</summary>
    None,

    /// <summary>The OK button.</summary>
    Ok,

    /// <summary>The Cancel button, or Escape in a box that has one.</summary>
    Cancel,

    /// <summary>The Yes button.</summary>
    Yes,

    /// <summary>The No button, or Escape in a Yes/No box.</summary>
    No,
}
