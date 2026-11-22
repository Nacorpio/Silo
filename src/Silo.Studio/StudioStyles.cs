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
            "#body": { "flex-direction": "row", "flex-grow": "1" },
            "#scroll": { "flex-grow": "1" },
            "#editor": {
              "width": "440px", "padding": "0px 16px 16px 8px", "gap": "8px", "flex-direction": "column"
            },
            "#editor .title": { "font-size": "15px", "font-weight": "bold" },
            "#editor .muted": { "color": "var(--muted)", "font-size": "12px" },
            "#editor-toolbar": { "flex-direction": "row", "gap": "6px" },
            "#editor-modes": { "flex-direction": "row", "gap": "6px" },
            "#editor-modes Button.selected": { "background": "var(--accent)", "color": "var(--accent-text)" },
            "#visual-view": { "gap": "8px", "flex-direction": "column" },
            "#editor-content": { "flex-direction": "column", "flex-grow": "1" },
            "#source": {
              "flex-grow": "1", "height": "380px", "font-family": "var(--font-mono)", "font-size": "12px",
              "background": "var(--surface)", "color": "var(--text)", "border-width": "1px", "border-color": "var(--border)",
              "border-radius": "4px", "caret-color": "var(--text)", "gutter-color": "var(--muted)",
              "&:focus": { "border-color": "var(--accent)" }
            },
            "#editor Button": {
              "background": "var(--track)", "color": "var(--text)", "padding": "3px 10px",
              "&:hover": { "background": "var(--border)" }
            },
            "#rules": { "height": "132px" },
            "#declarations": { "gap": "4px", "flex-direction": "column" },
            ".declaration": { "flex-direction": "row", "gap": "6px", "align-items": "center" },
            ".declaration-name": { "width": "96px", "font-size": "12px" },
            ".declaration TextBox": { "flex-grow": "1" },
            "#add-row": { "flex-direction": "row", "gap": "6px" },
            "#add-row TextBox": { "width": "100px" },
            "#add-rule-row": { "flex-direction": "row", "gap": "6px" },
            "#add-rule-row TextBox": { "width": "314px" },
            "#previews-panel": {
              "width": "340px", "padding": "0px 16px 16px 8px", "gap": "8px", "flex-direction": "column"
            },
            "#previews-panel .title": { "font-size": "15px", "font-weight": "bold" },
            "#previews-panel .muted": { "color": "var(--muted)", "font-size": "12px" },
            "#previews-toolbar": { "flex-direction": "row", "gap": "6px" },
            "#previews-panel Button": {
              "background": "var(--track)", "color": "var(--text)", "padding": "3px 10px",
              "&:hover": { "background": "var(--border)" }
            },
            "#previews": { "height": "200px" },
            "#matrix": { "padding": "0px 16px 16px 16px", "gap": "10px", "flex-direction": "column" },
            ".matrix-row": { "flex-direction": "row", "align-items": "center", "gap": "8px" },
            ".row-label": { "width": "110px", "color": "var(--muted)" },
            "Button.row-label": {
              "background": "var(--surface)", "padding": "2px 0px", "text-align": "left", "border-radius": "3px",
              "&:hover": { "background": "var(--track)", "color": "var(--text)" },
              "&:active": { "background": "var(--border)" }
            },
            ".column-title": { "width": "124px", "color": "var(--muted)", "font-size": "12px" },
            ".cell": { "width": "124px", "align-items": "start" },
            ".not-applicable": { "color": "var(--border)" },
            ".cell TextBox": { "width": "112px" },
            ".cell Slider": { "width": "112px" },
            ".cell ProgressBar": { "width": "112px" },
            ".cell ComboBox": { "width": "112px" },
            ".cell Separator": { "width": "112px" },
            ".cell Expander": { "width": "112px" },
            ".cell TabView": { "width": "130px" },
            ".cell Dialog": { "width": "200px", "min-width": "0px", "flex-shrink": "0" },
            ".cell ListView": { "width": "112px", "height": "66px" }
          }
        }
        """;

    /// <summary>Parses Studio's Stylesheet.</summary>
    /// <returns>The Stylesheet, to be applied after the theme.</returns>
    public static Stylesheet Load() => JsoncStylesheetReader.Read(Sheet, FileName);
}
