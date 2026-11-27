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
            "#review-window": { "gap": "0px" },
            "#review-split": { "flex-grow": "1" },
            "#review-left": { "flex-direction": "column", "gap": "12px", "padding": "8px 16px 16px 16px" },
            "#review-controls": { "flex-direction": "row", "gap": "12px", "align-items": "center" },
            "#review-stage": {
              "flex-grow": "1", "flex-direction": "column", "padding": "24px", "background": "var(--surface)", "border-width": "1px",
              "border-color": "var(--border)", "border-radius": "6px", "align-items": "flex-start"
            },
            "#review-stage ToolCallCard": { "width": "420px", "flex-shrink": "0" },
            "#review-stage ToolCallTimeline": { "width": "440px", "flex-shrink": "0" },
            "#review": { "flex-direction": "column", "gap": "10px", "padding": "8px 16px 16px 16px" },
            "#review .title": { "font-size": "16px", "font-weight": "bold" },
            "#review .muted": { "color": "var(--muted)", "font-size": "12px" },
            "#review-scroll": { "flex-grow": "1" },
            "#review-questions": { "flex-direction": "column", "gap": "18px", "padding": "0px 10px 0px 0px" },
            "#review .question": { "flex-direction": "column", "gap": "6px", "align-items": "flex-start" },
            "#review .prompt": { "font-weight": "bold" },
            "#review TextArea": {
              "width": "100%", "height": "72px", "background": "var(--surface)", "color": "var(--text)", "border-width": "1px",
              "border-color": "var(--border)", "border-radius": "4px", "caret-color": "var(--text)",
              "&:focus": { "border-color": "var(--accent)" }
            },
            "#review TextBox": { "width": "100%" },
            "#review .show": { "background": "var(--track)", "color": "var(--text)", "padding": "3px 10px", "&:hover": { "background": "var(--border)" } },
            "#review-footer": { "flex-direction": "row", "gap": "12px", "align-items": "center" },
            "#review-submit": { "background": "var(--accent)", "color": "var(--accent-text)", "padding": "6px 16px" },
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
            "#categories": { "width": "236px", "flex-shrink": "0", "flex-direction": "column" },
            ".category-items": { "flex-direction": "column", "gap": "8px", "padding": "12px 8px 16px 16px" },
            ".category": {
              "flex-direction": "row", "align-items": "center", "gap": "16px", "padding": "6px 8px", "border-radius": "4px",
              "background": "var(--surface)", "color": "var(--muted)", "text-align": "left",
              "&:hover": { "background": "var(--track)" },
              "&:selected": { "background": "var(--track)", "color": "var(--text)" }
            },
            ".category-text": { "flex-direction": "column", "flex-grow": "1", "align-items": "stretch" },
            ".category-heading": { "flex-direction": "row", "gap": "6px" },
            ".category-name": { "color": "var(--text)" },
            ".category-count": { "color": "var(--muted)", "font-size": "12px" },
            ".category-description": { "color": "var(--muted)", "font-size": "12px" },
            "#matrix": { "padding": "0px 16px 16px 16px", "gap": "10px", "flex-direction": "column" },
            ".matrix-page": { "padding": "12px 0px 0px 0px", "gap": "10px", "flex-direction": "column" },
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
            ".cell NumberBox": { "width": "112px" },
            ".cell PasswordBox": { "width": "112px" },
            ".cell Toolbar": { "width": "200px", "flex-shrink": "0" },
            ".cell Toolbar Separator": { "width": "1px", "height": "auto" },
            ".cell QuantityBox": { "width": "112px" },
            ".cell QuantityBox Separator": { "width": "1px", "height": "auto" },
            ".cell SearchBox": { "width": "112px" },
            ".cell TextArea": { "width": "112px", "height": "66px" },
            ".cell Slider": { "width": "112px" },
            ".cell ProgressBar": { "width": "112px" },
            ".cell ComboBox": { "width": "112px" },
            ".cell ColorPicker": { "width": "130px" },
            ".cell ColorPanel": { "flex-shrink": "0" },
            ".cell EmojiPanel": { "flex-shrink": "0" },
            ".cell ToolCallCard": { "width": "380px", "flex-shrink": "0" },
            ".cell ToolCallTimeline": { "width": "420px", "flex-shrink": "0" },
            ".cell .toasts": { "flex-direction": "column", "gap": "6px", "width": "330px", "height": "176px", "flex-shrink": "0" },
            ".cell .stack": { "flex-direction": "column", "gap": "4px" },
            ".cell .badges": { "flex-direction": "row", "gap": "6px", "align-items": "center", "width": "360px", "flex-shrink": "0" },
            ".cell .icons": { "flex-direction": "row", "gap": "8px", "width": "560px", "flex-shrink": "0" },
            ".cell .infobars": { "flex-direction": "column", "gap": "6px", "width": "340px", "flex-shrink": "0" },
            ".cell GroupBox": { "width": "150px", "flex-shrink": "0" },
            ".cell TitleBar": { "width": "330px", "flex-shrink": "0" },
            ".cell StatList": { "width": "140px", "flex-shrink": "0" },
            ".cell Card": { "width": "200px", "flex-shrink": "0" },
            ".cell SegmentedControl": { "width": "230px", "flex-shrink": "0" },
            ".cell RadioGroup": { "width": "112px" },
            ".cell Rating": { "flex-shrink": "0" },
            ".cell RangeSlider": { "width": "112px" },
            ".cell TokenBox": { "width": "250px", "flex-shrink": "0" },
            ".cell .forms": { "flex-direction": "column", "gap": "6px", "width": "200px", "flex-shrink": "0" },
            ".cell CheckedListBox": { "width": "112px", "height": "90px" },
            ".cell .tooltips": { "flex-direction": "column", "gap": "6px", "width": "330px", "flex-shrink": "0", "align-items": "start" },
            ".cell Breadcrumb": { "flex-shrink": "0" },
            ".cell Pager": { "flex-shrink": "0" },
            ".cell Accordion": { "width": "150px" },
            ".cell StatusBar": { "width": "330px", "flex-shrink": "0" },
            ".cell Stepper": { "width": "330px", "flex-shrink": "0" },
            ".cell Carousel": { "width": "200px", "flex-shrink": "0" },
            ".cell GalleryView": { "width": "330px", "height": "120px", "flex-shrink": "0", "item-width": "80px", "item-height": "70px", "gallery-gap": "6px", "gallery-padding": "6px" },
            ".cell NavigationView": { "width": "330px", "height": "170px", "flex-shrink": "0" },
            ".cell DatePicker": { "width": "130px" },
            ".cell Calendar": { "width": "230px", "flex-shrink": "0" },
            ".cell Separator": { "width": "112px" },
            ".cell Expander": { "width": "112px" },
            ".cell TabView": { "width": "170px", "flex-shrink": "0" },
            ".cell Dialog": { "width": "200px", "min-width": "0px", "flex-shrink": "0" },
            ".cell DataGrid": { "width": "210px", "height": "130px", "flex-shrink": "0" },
            ".cell ListView": { "width": "112px", "height": "66px" },
            ".cell TreeView": { "width": "124px", "height": "90px" },
            "#playground": { "flex-direction": "column", "gap": "14px", "padding": "16px", "max-width": "760px" },
            ".playground-section": { "flex-direction": "column", "gap": "8px" },
            ".playground-title": { "font-size": "14px", "font-weight": "bold" },
            ".playground-row": { "flex-direction": "row", "gap": "10px", "align-items": "center" },
            ".playground-label": { "color": "var(--muted)", "font-size": "12px", "width": "70px" },
            ".playground-chips": { "flex-direction": "row", "gap": "6px" },
            ".playground-output": { "flex-direction": "row", "gap": "8px", "align-items": "center" },
            ".code": { "font-family": "var(--font-mono)", "font-size": "12px", "flex-grow": "1" },
            "#stage": { "height": "120px", "background": "var(--track)", "border-radius": "6px", "padding": "12px" },
            "#track": { "flex-direction": "row", "flex-grow": "1", "align-items": "center" },
            ".stage-target": { "width": "56px", "height": "56px", "background": "var(--accent)", "border-radius": "8px" },
            "#duration": { "width": "260px" },
            "#delay": { "width": "260px" },
            "#curve": { "width": "160px", "height": "160px" },
            "#curve-parameters": { "flex-direction": "row", "gap": "8px" }
          }
        }
        """;

    /// <summary>Parses Studio's Stylesheet.</summary>
    /// <returns>The Stylesheet, to be applied after the theme.</returns>
    public static Stylesheet Load() => JsoncStylesheetReader.Read(Sheet, FileName);
}
