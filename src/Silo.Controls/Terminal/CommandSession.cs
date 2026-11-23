using System.Text;

namespace Silo;

/// <summary>
/// A session that is a small shell written in C#: it shows a prompt, lets the user edit a line with history, and runs the command
/// handlers that were added with <see cref="Add(string, string, Action{CommandContext})"/>.
/// </summary>
/// <remarks>
/// <para>
/// A line is split into a command name and arguments, where quotes group words. Handlers can be synchronous or asynchronous. While an
/// asynchronous handler runs the input waits, and Ctrl+C cancels <see cref="CommandContext.CancellationToken"/>. The commands
/// <c>help</c>, <c>clear</c> and <c>exit</c> exist from the start and can be replaced. An unknown command prints
/// <c>name: command not found</c>, and an exception in a handler prints its message in red.
/// </para>
/// <para>
/// The session writes its output from the thread that runs the handler, which is why <see cref="Terminal.Attach"/> takes a dispatcher.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var session = new CommandSession { Banner = "Welcome to the console.\n" };
///
/// session.Add("echo", "Prints its arguments", context => context.WriteLine(string.Join(' ', context.Arguments)));
/// session.Add("wait", "Waits a little", async context => await Task.Delay(1000, context.CancellationToken));
///
/// terminal.Attach(session);
/// </code>
/// </example>
public sealed class CommandSession : ITerminalSession
{
    readonly SortedDictionary<string, (string Description, Func<CommandContext, Task> Run)> commands = new(StringComparer.Ordinal);

    readonly TerminalLineEditor editor = new();

    readonly Queue<string> pending = new();

    CancellationTokenSource? running;

    bool busy;

    bool started;

    bool exited;

    /// <summary>Creates a session with the commands <c>help</c>, <c>clear</c> and <c>exit</c>.</summary>
    public CommandSession()
    {
        Add("help", "Lists the commands", context =>
        {
            var width = commands.Keys.Max(name => name.Length);

            foreach (var (name, (description, _)) in commands)
                context.WriteLine($"  \u001b[1m{name.PadRight(width)}\u001b[0m  {description}");
        });

        Add("clear", "Clears the screen", context => context.Clear());
        Add("exit", "Ends the session", _ => End(0));
    }

    /// <inheritdoc/>
    public event EventHandler<TerminalOutputEventArgs>? Output;

    /// <inheritdoc/>
    public event EventHandler<TerminalExitedEventArgs>? Exited;

    /// <summary>Gets or sets the text shown before each line. It can hold escape sequences. The default is <c>$ </c>.</summary>
    public string Prompt { get; set; } = "$ ";

    /// <summary>Gets or sets the text shown when the session starts. Line feeds start a new line at the left edge.</summary>
    public string Banner { get; set; } = string.Empty;

    /// <summary>Gets the number of columns of the terminal.</summary>
    public int Columns { get; private set; } = 80;

    /// <summary>Gets the number of rows of the terminal.</summary>
    public int Rows { get; private set; } = 24;

    /// <summary>Gets the line editor, which holds the history.</summary>
    public TerminalLineEditor Editor => editor;

    /// <summary>Adds a command, or replaces the one with the same name.</summary>
    /// <param name="name">The name, without spaces.</param>
    /// <param name="description">One line that <c>help</c> shows.</param>
    /// <param name="run">The handler.</param>
    public void Add(string name, string description, Action<CommandContext> run)
    {
        ArgumentNullException.ThrowIfNull(run);

        Add(name, description, context =>
        {
            run(context);

            return Task.CompletedTask;
        });
    }

    /// <summary>Adds an asynchronous command, or replaces the one with the same name.</summary>
    /// <param name="name">The name, without spaces.</param>
    /// <param name="description">One line that <c>help</c> shows.</param>
    /// <param name="run">The handler.</param>
    public void Add(string name, string description, Func<CommandContext, Task> run)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(run);

        commands[name] = (description, run);
    }

    /// <summary>Splits a line into words, where single or double quotes group text and a backslash makes the next character plain.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The words.</returns>
    public static IReadOnlyList<string> Split(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var words = new List<string>();
        var word = new StringBuilder();
        var (inWord, quote) = (false, '\0');

        for (var i = 0; i < line.Length; i++)
        {
            var character = line[i];

            if (quote != '\0')
            {
                if (character == quote)
                    quote = '\0';
                else
                    word.Append(character);

                continue;
            }

            if (character is '"' or '\'')
            {
                (quote, inWord) = (character, true);

                continue;
            }

            if (character == '\\' && i + 1 < line.Length)
            {
                word.Append(line[++i]);

                inWord = true;

                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                if (inWord)
                    words.Add(word.ToString());

                word.Clear();

                inWord = false;

                continue;
            }

            word.Append(character);

            inWord = true;
        }

        if (inWord)
            words.Add(word.ToString());

        return words;
    }

    /// <inheritdoc/>
    public void Start(int columns, int rows)
    {
        (Columns, Rows) = (columns, rows);

        if (started)
            return;

        started = true;

        Emit(Banner);
        Emit(Prompt);
    }

    /// <inheritdoc/>
    public void Resize(int columns, int rows) => (Columns, Rows) = (columns, rows);

    /// <inheritdoc/>
    public void Send(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (exited)
            return;

        if (busy)
        {
            if (text.Contains('\u0003'))
            {
                Emit("^C\r\n");

                running?.Cancel();
            }

            return;
        }

        var edit = editor.Feed(text);

        Emit(edit.Output);

        if (edit.ClearRequested)
            Emit("\u001b[H\u001b[2J" + Prompt + editor.Redraw());

        if (edit.Interrupted)
            Emit(Prompt);

        if (edit.EndOfInput)
        {
            Emit("exit\r\n");
            End(0);

            return;
        }

        foreach (var line in edit.Lines)
            pending.Enqueue(line);

        if (pending.Count > 0)
            _ = Drain();
        else if (edit.Lines.Count > 0)
            Emit(Prompt);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        exited = true;

        running?.Cancel();
    }

    /// <summary>Gives text to the terminal, translating line feeds.</summary>
    /// <param name="text">The text.</param>
    internal void Emit(string text)
    {
        if (text.Length == 0)
            return;

        text = text.Replace("\r\n", "\n").Replace("\n", "\r\n");

        Output?.Invoke(this, TerminalOutputEventArgs.FromText(text));
    }

    /// <summary>Ends the session once.</summary>
    /// <param name="exitCode">The exit code.</param>
    void End(int exitCode)
    {
        if (exited)
            return;

        exited = true;

        Exited?.Invoke(this, new TerminalExitedEventArgs(exitCode));
    }

    /// <summary>Runs the queued lines one after the other and shows the prompt after them.</summary>
    async Task Drain()
    {
        busy = true;

        try
        {
            while (pending.TryDequeue(out var line) && !exited)
                await RunAsync(line);
        }
        finally
        {
            busy = false;
            running = null;
        }

        if (!exited)
            Emit(Prompt);
    }

    /// <summary>Runs one line.</summary>
    /// <param name="line">The line.</param>
    async Task RunAsync(string line)
    {
        var words = Split(line);

        if (words.Count == 0)
            return;

        var name = words[0];

        if (!commands.TryGetValue(name, out var command))
        {
            Emit($"{name}: command not found\n");

            return;
        }

        using var source = new CancellationTokenSource();

        running = source;

        try
        {
            await command.Run(new CommandContext(this, name, line, words.Skip(1).ToArray(), source.Token));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Emit($"\u001b[31m{name}: {exception.Message}\u001b[0m\n");
        }
    }
}
