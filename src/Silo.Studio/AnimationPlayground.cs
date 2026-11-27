using System.Globalization;
using Silo.Styling.Values;

namespace Silo.Studio;

/// <summary>The properties that the animation playground can animate. They can be combined.</summary>
[Flags]
public enum PlaygroundProperties
{
    /// <summary>No property.</summary>
    None = 0,

    /// <summary>The target slides to the right, with <c>transform</c>.</summary>
    Move = 1,

    /// <summary>The target turns half a circle, with <c>transform</c>.</summary>
    Rotate = 2,

    /// <summary>The target grows, with <c>transform</c>.</summary>
    Scale = 4,

    /// <summary>The target fades, with <c>opacity</c>.</summary>
    Fade = 8,

    /// <summary>The target changes color, with <c>background</c>.</summary>
    Color = 16,

    /// <summary>The target gets wider, with <c>width</c>, which changes the layout.</summary>
    Size = 32,
}

/// <summary>The timing curves that the animation playground offers.</summary>
public enum PlaygroundCurve
{
    /// <summary>An even change.</summary>
    Linear,

    /// <summary>The default curve.</summary>
    Ease,

    /// <summary>Starts slowly.</summary>
    EaseIn,

    /// <summary>Ends slowly.</summary>
    EaseOut,

    /// <summary>Starts and ends slowly.</summary>
    EaseInOut,

    /// <summary>A curve with four numbers of your own.</summary>
    CubicBezier,

    /// <summary>Jumps in equal steps.</summary>
    Steps,

    /// <summary>A damped spring, which has no duration.</summary>
    Spring,
}

/// <summary>
/// The page of the Animations tab of Studio: a stage with a target, controls for what animates and how, a plot of the curve, and the
/// <c>transition</c> text to copy into a Stylesheet. Pressing Play moves the target between its two states with a real transition, so what is
/// shown is exactly what an app gets.
/// </summary>
/// <remarks>
/// <para>
/// The stage runs on its own clock, so the speed (1x, one half, one quarter, one tenth) slows the animation down without touching the rest of
/// Studio. With <see cref="Loop"/> the target goes back and forth by itself.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var playground = new AnimationPlayground
/// {
///     Properties = PlaygroundProperties.Move | PlaygroundProperties.Fade,
///     Curve = PlaygroundCurve.EaseOut,
///     DurationMilliseconds = 300,
/// };
///
/// playground.Play();
///
/// Console.WriteLine(playground.TransitionText);   // transform 300ms ease-out, opacity 300ms ease-out
/// </code>
/// </example>
public sealed class AnimationPlayground : Box
{
    /// <summary>How long the target rests at each end while looping, in milliseconds.</summary>
    const double HoldMilliseconds = 500;

    /// <summary>The numbers for the speed buttons, in the order of <see cref="SpeedLabels"/>.</summary>
    static readonly double[] Speeds = [1, 0.5, 0.25, 0.1];

    /// <summary>The labels of the speed buttons.</summary>
    static readonly string[] SpeedLabels = ["1×", "½×", "¼×", "0.1×"];

    /// <summary>The names of the curves, in the order of <see cref="PlaygroundCurve"/>.</summary>
    static readonly string[] CurveLabels = ["Linear", "Ease", "Ease in", "Ease out", "In-out", "Bézier", "Steps", "Spring"];

    /// <summary>The stage, which holds the target and runs on its own clock.</summary>
    readonly Box stage = new() { Name = "stage" };

    /// <summary>The Control that animates.</summary>
    readonly Box target = new() { Name = "stage-target" };

    /// <summary>The plot of the curve.</summary>
    readonly EasingCurve plot = new() { Name = "curve" };

    /// <summary>The clock of the stage, which can run slower than the real one.</summary>
    readonly ScaledAnimationClock clock;

    /// <summary>The text of the transition.</summary>
    readonly TextBlock output = new() { Name = "transition-text" };

    /// <summary>The place that holds the numbers of the curve that is selected.</summary>
    readonly Box parameters = new() { Name = "curve-parameters" };

    /// <summary>The words next to the duration and the delay.</summary>
    readonly TextBlock durationLabel = new();

    /// <summary>The words next to the delay.</summary>
    readonly TextBlock delayLabel = new();

    /// <summary>The check boxes of the properties.</summary>
    readonly Dictionary<PlaygroundProperties, CheckBox> propertyBoxes = [];

    /// <summary>The buttons of the curves.</summary>
    readonly SegmentedControl curves = new() { Name = "curves", Items = CurveLabels };

    /// <summary>The slider of the duration.</summary>
    readonly Slider duration = new() { Name = "duration", Minimum = 0, Maximum = 2000, Value = 400 };

    /// <summary>The slider of the delay.</summary>
    readonly Slider delay = new() { Name = "delay", Minimum = 0, Maximum = 1000, Value = 0 };

    /// <summary>The number boxes of the Bézier curve, then of the steps and of the spring.</summary>
    readonly NumberBox[] numbers = new NumberBox[8];

    /// <summary>The properties that animate.</summary>
    PlaygroundProperties properties = PlaygroundProperties.Move | PlaygroundProperties.Fade;

    /// <summary>The curve.</summary>
    PlaygroundCurve curve = PlaygroundCurve.EaseOut;

    /// <summary>Whether the target is at its second state.</summary>
    bool atEnd;

    /// <summary>Whether the target goes back and forth by itself.</summary>
    bool loop;

    /// <summary>The time on the stage clock when the running transition started, after its delay, or <see langword="null"/> when none runs.</summary>
    TimeSpan? playStart;

    /// <summary>The time on the stage clock after which a looping target moves again.</summary>
    TimeSpan? holdUntil;

    /// <summary>Runs before the first playground is created, so that the Styled properties are registered in time.</summary>
    static AnimationPlayground()
    {
    }

    /// <summary>Creates the playground with the target moving and fading with an ease-out curve.</summary>
    public AnimationPlayground()
    {
        Name = "playground";

        clock = new ScaledAnimationClock(() => Parent?.AnimationClock);
        stage.AnimationClock = clock;

        BuildStage();
        BuildControls();

        Children.Add(stage);
        Children.Add(Section("Properties", Row("Animate", PropertyRow())));
        Children.Add(Section("Timing", CurveRow(), PresetRow(), parameters, Row("Duration", duration, durationLabel), Row("Delay", delay, delayLabel)));
        Children.Add(Section("Result", OutputRow()));
        Children.Add(Section("Playback", PlaybackRow()));

        Refresh();
        ShowParameters();
        Reset();
    }

    /// <summary>Gets the Control that animates.</summary>
    public Control Target => target;

    /// <summary>Gets the plot of the curve.</summary>
    public EasingCurve Plot => plot;

    /// <summary>Gets or sets what the playground uses to copy the transition text.</summary>
    public IClipboard? Clipboard { get; set; }

    /// <summary>Gets the clock of the stage, whose speed and pause the playback buttons change.</summary>
    public ScaledAnimationClock Clock => clock;

    /// <summary>Gets or sets which properties animate. Changing it puts the target back at its first state.</summary>
    public PlaygroundProperties Properties
    {
        get => properties;
        set
        {
            properties = value;

            foreach (var (flag, box) in propertyBoxes)
                box.IsChecked = value.HasFlag(flag);

            Refresh();
            Reset();
        }
    }

    /// <summary>Gets or sets the timing curve.</summary>
    public PlaygroundCurve Curve
    {
        get => curve;
        set
        {
            curve = value;
            curves.SelectedIndex = (int)value;

            ShowParameters();
            Refresh();
        }
    }

    /// <summary>Gets or sets how long a fixed curve takes, in milliseconds, from 0 to 2000. A spring ignores it.</summary>
    public double DurationMilliseconds
    {
        get => Math.Round(duration.Value);
        set
        {
            duration.Value = value;

            Refresh();
        }
    }

    /// <summary>Gets or sets the wait before the animation starts, in milliseconds, from 0 to 1000.</summary>
    public double DelayMilliseconds
    {
        get => Math.Round(delay.Value);
        set
        {
            delay.Value = value;

            Refresh();
        }
    }

    /// <summary>Gets or sets whether the target goes back and forth by itself. The default is <see langword="false"/>.</summary>
    public bool Loop
    {
        get => loop;
        set
        {
            loop = value;
            holdUntil = null;
        }
    }

    /// <summary>Gets whether the target is at its second state.</summary>
    public bool IsAtEnd => atEnd;

    /// <summary>Gets the motion that the settings describe.</summary>
    public Motion Motion
    {
        get
        {
            var time = TimeSpan.FromMilliseconds(DurationMilliseconds);

            return curve switch
            {
                PlaygroundCurve.Linear => new Motion(time, Easing.Linear),
                PlaygroundCurve.Ease => new Motion(time, Easing.Ease),
                PlaygroundCurve.EaseIn => new Motion(time, Easing.EaseIn),
                PlaygroundCurve.EaseOut => new Motion(time, Easing.EaseOut),
                PlaygroundCurve.EaseInOut => new Motion(time, Easing.EaseInOut),
                PlaygroundCurve.CubicBezier => new Motion(time, Easing.CubicBezier(numbers[0].Value, numbers[1].Value, numbers[2].Value, numbers[3].Value)),
                PlaygroundCurve.Steps => new Motion(time, Easing.Steps((int)numbers[4].Value)),

                _ => new Motion(new Spring(numbers[5].Value, numbers[6].Value, numbers[7].Value)),
            };
        }
    }

    /// <summary>Gets the transition that the settings describe, with one entry for each property that is animated.</summary>
    public TransitionList Transition
    {
        get
        {
            var names = new List<string>();

            if (properties.HasFlag(PlaygroundProperties.Move) || properties.HasFlag(PlaygroundProperties.Rotate) || properties.HasFlag(PlaygroundProperties.Scale))
                names.Add("transform");

            if (properties.HasFlag(PlaygroundProperties.Fade))
                names.Add("opacity");

            if (properties.HasFlag(PlaygroundProperties.Color))
                names.Add("background");

            if (properties.HasFlag(PlaygroundProperties.Size))
                names.Add("width");

            var motion = Motion;
            var wait = TimeSpan.FromMilliseconds(DelayMilliseconds);

            return new TransitionList([.. names.Select(name => new TransitionSpec(name, motion, wait))]);
        }
    }

    /// <summary>Gets the text to put after <c>"transition":</c> in a Stylesheet, such as <c>transform 300ms ease-out, opacity 300ms ease-out</c>.</summary>
    public string TransitionText => Transition.ToString();

    /// <summary>Moves the target to its other state with the transition that the settings describe.</summary>
    public void Play()
    {
        atEnd = !atEnd;
        holdUntil = null;

        target.SetValue(Element.TransitionProperty, Transition);
        ApplyValues();

        var wait = TimeSpan.FromMilliseconds(DelayMilliseconds);

        playStart = stage.AnimationTime + wait;
    }

    /// <summary>Puts the target back at its first state at once.</summary>
    public void Reset()
    {
        atEnd = false;
        playStart = null;

        target.SetValue(Element.TransitionProperty, TransitionList.None);
        ApplyValues();
        target.SetValue(Element.TransitionProperty, Transition);
        plot.Marker = null;
    }

    /// <summary>Gets whether the playground wants frames: while it loops.</summary>
    public override bool IsAnimating => loop;

    /// <inheritdoc/>
    protected override bool OnFrame()
    {
        UpdateMarker();

        if (!loop || target.HasActiveTransitions)
            return false;

        var now = stage.AnimationTime;

        holdUntil ??= now + TimeSpan.FromMilliseconds(HoldMilliseconds);

        if (now >= holdUntil)
            Play();

        return false;
    }

    /// <summary>Moves the marker of the plot to where the running animation is.</summary>
    void UpdateMarker()
    {
        if (playStart is not { } start || !target.HasActiveTransitions)
        {
            plot.Marker = null;
            playStart = target.HasActiveTransitions ? playStart : null;

            return;
        }

        var motion = Motion;
        var total = motion.DurationFor();
        var elapsed = stage.AnimationTime - start;

        plot.Marker = total <= TimeSpan.Zero ? null : Math.Max(0, elapsed / total);
    }

    /// <summary>Sets the values of the animated properties for the current state.</summary>
    void ApplyValues()
    {
        var move = properties.HasFlag(PlaygroundProperties.Move) && atEnd ? 240.0 : 0;
        var rotate = properties.HasFlag(PlaygroundProperties.Rotate) && atEnd ? 180.0 : 0;
        var scale = properties.HasFlag(PlaygroundProperties.Scale) && atEnd ? 1.6 : 1;

        target.SetValue(Control.TransformProperty, new Transform(move, 0, rotate, scale, scale));
        target.SetValue(Element.OpacityProperty, properties.HasFlag(PlaygroundProperties.Fade) && atEnd ? 0.15 : 1);
        target.SetValue(Control.BackgroundProperty, properties.HasFlag(PlaygroundProperties.Color) && atEnd ? new Color(236, 72, 153) : new Color(59, 130, 246));
        target.SetValue(Control.WidthProperty, new Length(properties.HasFlag(PlaygroundProperties.Size) && atEnd ? 160 : 72, LengthUnit.Px));
    }

    /// <summary>Updates the plot, the labels and the transition text after a setting changed.</summary>
    void Refresh()
    {
        plot.Motion = Motion;
        durationLabel.Text = curve == PlaygroundCurve.Spring ? "springs ignore the duration" : $"{DurationMilliseconds.ToString("0", CultureInfo.InvariantCulture)} ms";
        delayLabel.Text = $"{DelayMilliseconds.ToString("0", CultureInfo.InvariantCulture)} ms";
        output.Text = properties == PlaygroundProperties.None ? "none" : TransitionText;
    }

    /// <summary>Shows the number boxes that belong to the selected curve.</summary>
    void ShowParameters()
    {
        parameters.Children.Clear();

        switch (curve)
        {
            case PlaygroundCurve.CubicBezier:
                parameters.Children.Add(Row("x1  y1  x2  y2", numbers[0], numbers[1], numbers[2], numbers[3]));
                break;

            case PlaygroundCurve.Steps:
                parameters.Children.Add(Row("Steps", numbers[4]));
                break;

            case PlaygroundCurve.Spring:
                parameters.Children.Add(Row("Stiffness  Damping  Mass", numbers[5], numbers[6], numbers[7]));
                break;
        }
    }

    /// <summary>Builds the stage with the target on it.</summary>
    void BuildStage()
    {
        var track = new Box { Name = "track" };

        target.AddClass("stage-target");
        track.Children.Add(target);
        stage.Children.Add(track);
    }

    /// <summary>Creates the controls and connects them.</summary>
    void BuildControls()
    {
        double[] first = [0.25, 0.1, 0.25, 1, 4, 170, 18, 1];

        for (var i = 0; i < numbers.Length; i++)
        {
            numbers[i] = new NumberBox { Value = first[i], Step = i < 4 ? 0.05 : i == 4 ? 1 : i == 7 ? 0.1 : 5, Minimum = i switch { 1 or 3 => -3, 4 => 1, 5 => 1, 6 => 0, 7 => 0.1, _ => 0 }, Maximum = i switch { 0 or 2 => 1, 1 or 3 => 4, 4 => 30, 5 => 1000, 6 => 200, _ => 10 } };
            numbers[i].ValueChanged += (_, _) => Refresh();
        }

        curves.SelectedIndex = (int)curve;
        curves.SelectionChanged += (_, _) => Curve = (PlaygroundCurve)Math.Max(0, curves.SelectedIndex);

        duration.ValueChanged += (_, _) => Refresh();
        delay.ValueChanged += (_, _) => Refresh();

        foreach (var flag in new[] { PlaygroundProperties.Move, PlaygroundProperties.Rotate, PlaygroundProperties.Scale, PlaygroundProperties.Fade, PlaygroundProperties.Color, PlaygroundProperties.Size })
        {
            var box = new CheckBox { Text = flag.ToString(), IsChecked = properties.HasFlag(flag) };

            box.CheckedChanged += (_, _) => Properties = box.IsChecked ? properties | flag : properties & ~flag;

            propertyBoxes[flag] = box;
        }
    }

    /// <summary>Builds the row of property check boxes.</summary>
    /// <returns>The row.</returns>
    Box PropertyRow()
    {
        var row = new Box();

        row.AddClass("playground-chips");

        foreach (var box in propertyBoxes.Values)
            row.Children.Add(box);

        return row;
    }

    /// <summary>Builds the row of curve buttons and the preset buttons.</summary>
    /// <returns>The row.</returns>
    Box CurveRow()
    {
        return Row("Curve", curves);
    }

    /// <summary>Builds the row of presets.</summary>
    /// <returns>The row.</returns>
    Box PresetRow()
    {
        var presets = new Box();

        presets.AddClass("playground-chips");

        presets.Children.Add(Preset("Snappy", PlaygroundCurve.EaseOut, 200));
        presets.Children.Add(Preset("Smooth", PlaygroundCurve.EaseInOut, 450));
        presets.Children.Add(Preset("Bounce", PlaygroundCurve.Spring, 0, stiffness: 300, damping: 9));
        presets.Children.Add(Preset("Overshoot", PlaygroundCurve.CubicBezier, 500, bezier: [0.3, 1.8, 0.6, 1]));
        presets.Children.Add(Preset("Slow", PlaygroundCurve.Linear, 1500));

        return Row("Presets", presets);
    }

    /// <summary>Creates a button that applies a combination of curve and duration.</summary>
    /// <param name="text">The name.</param>
    /// <param name="preset">The curve.</param>
    /// <param name="milliseconds">The duration, or 0 to keep it.</param>
    /// <param name="stiffness">The stiffness of a spring.</param>
    /// <param name="damping">The damping of a spring.</param>
    /// <param name="bezier">The four numbers of a Bézier curve.</param>
    /// <returns>The button.</returns>
    Button Preset(string text, PlaygroundCurve preset, double milliseconds, double stiffness = 170, double damping = 18, double[]? bezier = null)
    {
        var button = new Button { Text = text };

        button.Click += (_, _) =>
        {
            if (milliseconds > 0)
                duration.Value = milliseconds;

            numbers[5].Value = stiffness;
            numbers[6].Value = damping;

            for (var i = 0; bezier is not null && i < 4; i++)
                numbers[i].Value = bezier[i];

            Curve = preset;
        };

        return button;
    }

    /// <summary>Builds the row with the transition text and the copy button.</summary>
    /// <returns>The row.</returns>
    Box OutputRow()
    {
        var row = new Box();
        var copy = new Button { Text = "Copy" };

        row.AddClass("playground-output");
        output.AddClass("code");

        copy.Click += (_, _) => Clipboard?.SetText(output.Text);

        row.Children.Add(plot);
        row.Children.Add(output);
        row.Children.Add(copy);

        return row;
    }

    /// <summary>Builds the row with Play, Reset, Loop and the speed.</summary>
    /// <returns>The row.</returns>
    Box PlaybackRow()
    {
        var row = new Box();
        var play = new Button { Text = "Play" };
        var reset = new Button { Text = "Reset" };
        var repeat = new CheckBox { Text = "Loop" };
        var speed = new SegmentedControl { Items = SpeedLabels, SelectedIndex = 0 };

        row.AddClass("playground-chips");
        play.AddClass("primary");

        play.Click += (_, _) => Play();
        reset.Click += (_, _) => Reset();
        repeat.CheckedChanged += (_, _) => Loop = repeat.IsChecked;
        speed.SelectionChanged += (_, _) => clock.Speed = Speeds[Math.Max(0, speed.SelectedIndex)];

        row.Children.Add(play);
        row.Children.Add(reset);
        row.Children.Add(repeat);
        row.Children.Add(new TextBlock { Text = "Speed" });
        row.Children.Add(speed);

        return row;
    }

    /// <summary>Builds a labelled section.</summary>
    /// <param name="title">The title.</param>
    /// <param name="rows">What it holds.</param>
    /// <returns>The section.</returns>
    static Box Section(string title, params Control[] rows)
    {
        var section = new Box();
        var heading = new TextBlock { Text = title };

        section.AddClass("playground-section");
        heading.AddClass("playground-title");
        section.Children.Add(heading);

        foreach (var row in rows)
            section.Children.Add(row);

        return section;
    }

    /// <summary>Builds a row with a label and controls.</summary>
    /// <param name="label">The label.</param>
    /// <param name="controls">The controls.</param>
    /// <returns>The row.</returns>
    static Box Row(string label, params Control[] controls)
    {
        var row = new Box();
        var text = new TextBlock { Text = label };

        row.AddClass("playground-row");
        text.AddClass("playground-label");
        row.Children.Add(text);

        foreach (var control in controls)
            row.Children.Add(control);

        return row;
    }
}
