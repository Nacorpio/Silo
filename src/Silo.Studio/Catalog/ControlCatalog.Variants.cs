
namespace Silo.Studio;

public static partial class ControlCatalog
{
    /// <summary>Gets the samples that the review window can switch between for a Control.</summary>
    /// <param name="name">The name of the entry.</param>
    /// <returns>The samples with their names. The first is the default. A Control without special samples has one, <c>Default</c>.</returns>
    /// <exception cref="ArgumentException">There is no entry with that name.</exception>
    public static IReadOnlyList<(string Name, Func<Control> Create)> VariantsOf(string name)
    {
        var entry = Entries.FirstOrDefault(e => e.Name == name) ?? throw new ArgumentException($"There is no Control named '{name}'.", nameof(name));

        return name switch
        {
            "ToolCallCard" =>
            [
                ("Succeeded", entry.Create),
                ("Running", () => CardWith(ToolCallStatus.Running, call => call.Output = "Running 1,139 tests...\n")),
                ("Failed", () => CardWith(ToolCallStatus.Failed, call => call.Error = "error CS0103: The name 'x' does not exist")),
                ("Awaiting approval", () => CardWith(ToolCallStatus.AwaitingApproval, _ => { })),
                ("Compact", () => Dense(entry.Create(), ToolCallDensity.Compact)),
                ("Roomy", () => Dense(entry.Create(), ToolCallDensity.Roomy)),
            ],
            "ToolCallTimeline" =>
            [
                ("Mixed", entry.Create),
                ("Empty", () => new ToolCallTimeline { TimeProvider = new FrozenTime(SampleNow) }),
                ("Compact", () => Dense(entry.Create(), ToolCallDensity.Compact)),
                ("Roomy", () => Dense(entry.Create(), ToolCallDensity.Roomy)),
            ],
            "ChatInput" =>
            [
                ("Empty", entry.Create),
                ("Written", CreateWrittenChatInput),
                ("Replying", CreateReplyChatInput),
                ("Send when text", () => WithSend(CreateWrittenChatInput(), SendButtonMode.WhenText)),
                ("No send button", () => WithSend(CreateWrittenChatInput(), SendButtonMode.Never)),
                ("Compact", () => Dense(CreateWrittenChatInput(), ChatDensity.Compact)),
                ("Roomy", () => Dense(CreateWrittenChatInput(), ChatDensity.Roomy)),
            ],
            _ => [("Default", entry.Create)],
        };
    }

    /// <summary>Gives a card or a timeline a density.</summary>
    /// <param name="control">The sample.</param>
    /// <param name="density">The density.</param>
    /// <returns>The same control.</returns>
    static Control Dense(Control control, ToolCallDensity density)
    {
        if (control is ToolCallCard card)
            card.Density = density;
        else if (control is ToolCallTimeline timeline)
            timeline.Density = density;

        return control;
    }

    /// <summary>Creates a card for a call in a status.</summary>
    /// <param name="status">The status.</param>
    /// <param name="configure">Fills in the rest of the call.</param>
    /// <returns>The card.</returns>
    static ToolCallCard CardWith(ToolCallStatus status, Action<ToolCall> configure)
    {
        var call = new ToolCall("run_tests", """{ "filter": "Silo.Controls" }""") { StartedAt = SampleNow - TimeSpan.FromSeconds(2.4) };

        configure(call);

        call.Status = status;

        if (status == ToolCallStatus.Failed)
            call.FinishedAt = SampleNow;

        return new ToolCallCard { Call = call, TimeProvider = new FrozenTime(SampleNow) };
    }
}
