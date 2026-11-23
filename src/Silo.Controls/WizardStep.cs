namespace Silo;

/// <summary>One step of a <see cref="Stepper"/>: a title for the header and what to show while the step is current.</summary>
/// <param name="Title">The title in the header.</param>
/// <param name="Content">The Control shown below the header while this is the current step.</param>
/// <example>
/// <code language="csharp">
/// var step = new WizardStep("Account", accountForm);
/// </code>
/// </example>
public sealed record WizardStep(string Title, Control Content);
