using Silo;
using Silo.Styling;
using Silo.Styling.Values;

/// <summary>An Element with a few Styled properties and a Pseudo-state switch, for tests.</summary>
public class Probe : Element
{
    public static readonly StyledProperty<Color> BackgroundProperty =
        StyledProperty.Register<Probe, Color>("background", default, Color.TryParse);
    public static readonly StyledProperty<Color> ForegroundProperty =
        StyledProperty.Register<Probe, Color>("color", new Color(0, 0, 0), Color.TryParse, inherits: true);

    static Probe()
    {
    }

    public List<StyledProperty> Changes { get; } = [];

    public Color Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public bool SetHover(bool value) => SetPseudoState("hover", value);

    protected override void OnPropertyChanged(StyledProperty property) => Changes.Add(property);
}
