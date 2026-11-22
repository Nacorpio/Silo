using Silo.Styling;

namespace Silo.Studio;

/// <summary>Studio's own layout rules. They use the theme's Tokens for color, so Studio follows the theme variant.</summary>
public static class StudioStyles
{
    /// <summary>The name that the Stylesheet's source positions report.</summary>
    public const string FileName = "studio.jsonc";

    /// <summary>The Stylesheet text.</summary>
    const string Sheet = """
        {
          "rules": {
            "#studio": { "gap": "0px" },
            "#bar": { "flex-direction": "row", "gap": "8px", "padding": "10px 16px", "align-items": "center" },
            "#bar .title": { "font-size": "18px", "font-weight": "bold", "flex-grow": "1" },
            "#bar Button": {
              "background": "var(--track)", "color": "var(--text)", "padding": "4px 12px",
              "&:hover": { "background": "var(--border)" },
              "&.selected": { "background": "var(--accent)", "color": "var(--accent-text)" }
            },
            "#scroll": { "flex-grow": "1" },
            "#matrix": { "padding": "0px 16px 16px 16px", "gap": "10px", "flex-direction": "column" },
            ".matrix-row": { "flex-direction": "row", "align-items": "center", "gap": "8px" },
            ".row-label": { "width": "110px", "color": "var(--muted)" },
            ".column-title": { "width": "124px", "color": "var(--muted)", "font-size": "12px" },
            ".cell": { "width": "124px", "align-items": "start" },
            ".not-applicable": { "color": "var(--border)" },
            ".cell TextBox": { "width": "112px" },
            ".cell Slider": { "width": "112px" },
            ".cell ProgressBar": { "width": "112px" },
            ".cell ComboBox": { "width": "112px" },
            ".cell ListView": { "width": "112px", "height": "66px" }
          }
        }
        """;

    /// <summary>Parses Studio's Stylesheet.</summary>
    /// <returns>The Stylesheet, to be applied after the theme.</returns>
    public static Stylesheet Load() => JsoncStylesheetReader.Read(Sheet, FileName);
}
