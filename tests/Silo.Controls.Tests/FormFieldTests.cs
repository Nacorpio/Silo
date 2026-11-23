using Silo;
using Xunit;

public class FormFieldTests
{
    [Fact]
    public void The_parts_are_stacked_label_input_then_help()
    {
        var input = new TextBox();
        var field = new FormField { Label = "Name", Content = input, HelpText = "Your full name" };

        Assert.Equal(3, field.Children.Count);
        Assert.Same(input, field.Children[1]);
        Assert.Contains("label", ((Control)field.Children[0]).Classes);
        Assert.Contains("help", ((Control)field.Children[2]).Classes);
    }

    [Fact]
    public void An_error_replaces_the_help_text_and_marks_the_field_invalid()
    {
        var field = new FormField { Label = "Age", Content = new NumberBox(), HelpText = "In years" };

        field.Error = "Too young";

        Assert.Contains("invalid", field.Classes);
        Assert.Equal(3, field.Children.Count);
        Assert.IsType<ValidationMessage>(field.Children[2]);

        field.Error = string.Empty;

        Assert.DoesNotContain("invalid", field.Classes);
        Assert.Contains("help", ((Control)field.Children[2]).Classes);
    }

    [Fact]
    public void Parts_without_text_are_left_out()
    {
        var field = new FormField { Content = new TextBox() };

        Assert.Single(field.Children);
    }

    [Fact]
    public void The_label_names_the_input_and_can_be_required()
    {
        var input = new TextBox();
        var field = new FormField { Label = "Name", Content = input, IsRequired = true };
        var label = (Label)field.Children[0];

        Assert.Same(input, label.For);
        Assert.True(label.IsRequired);

        field.IsRequired = false;

        Assert.False(label.IsRequired);
    }
}
