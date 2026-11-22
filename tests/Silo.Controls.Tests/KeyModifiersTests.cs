using Silo;
using Xunit;

public class KeyModifiersTests
{
    sealed class Recorder : Control
    {
        public List<(Key, KeyModifiers)> Seen { get; } = [];

        public override bool IsFocusable => true;

        public override bool KeyDown(Key key, KeyModifiers modifiers)
        {
            Seen.Add((key, modifiers));

            return true;
        }
    }

    sealed class Plain : Control
    {
        public List<Key> Seen { get; } = [];

        public override bool KeyDown(Key key)
        {
            Seen.Add(key);

            return true;
        }
    }

    [Fact]
    public void A_Control_that_only_overrides_the_plain_KeyDown_still_gets_the_key_with_modifiers()
    {
        var plain = new Plain();

        var changed = plain.KeyDown(Key.Tab, KeyModifiers.Shift);

        Assert.True(changed);
        Assert.Equal([Key.Tab], plain.Seen);
    }

    [Fact]
    public void The_focus_manager_sends_the_modifiers_to_the_focused_Control()
    {
        var recorder = new Recorder();
        var focus = new FocusManager();

        focus.Focus(recorder);

        focus.KeyDown(Key.Z, KeyModifiers.Control | KeyModifiers.Shift);
        focus.KeyDown(Key.Tab);

        Assert.Equal([(Key.Z, KeyModifiers.Control | KeyModifiers.Shift), (Key.Tab, KeyModifiers.None)], recorder.Seen);
    }
}
