using Silo.Diagnostics;

namespace Silo.Inspector;

/// <summary>
/// Connects an <see cref="InspectorView"/> to a running app: it asks the diagnostics agent for the tree and for the
/// properties of the Element that the user selects.
/// </summary>
/// <remarks>
/// The client's calls complete on thread-pool threads, so every change to the view goes through the <c>post</c> function
/// that the host passes in, which must run it on the thread that owns the view.
/// </remarks>
/// <example>
/// <code language="csharp">
/// await using var client = await DiagnosticsClient.ConnectAsync(pipeName);
///
/// var controller = new InspectorController(client, view, work => window.Post(work));
///
/// await controller.RefreshAsync();
/// </code>
/// </example>
public sealed class InspectorController
{
    /// <summary>The connection to the app.</summary>
    readonly DiagnosticsClient client;

    /// <summary>The view to fill.</summary>
    readonly InspectorView view;

    /// <summary>Runs work on the view's thread.</summary>
    readonly Action<Action> post;

    /// <summary>The nodes that the last snapshot returned, by id.</summary>
    readonly Dictionary<int, InspectorNode> nodes = [];

    /// <summary>Creates a controller and starts listening for selections.</summary>
    /// <param name="client">The connection to the app.</param>
    /// <param name="view">The view to fill.</param>
    /// <param name="post">Runs the given work on the view's thread.</param>
    public InspectorController(DiagnosticsClient client, InspectorView view, Action<Action> post)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(post);

        this.client = client;
        this.view = view;
        this.post = post;

        view.NodeSelected += id => _ = SelectAsync(id);
    }

    /// <summary>Fetches the Element tree again and shows it.</summary>
    /// <returns>A task that completes when the view has been updated.</returns>
    public async Task RefreshAsync()
    {
        var root = InspectorNode.Parse(await client.CallAsync("tree.snapshot"));

        post(() =>
        {
            nodes.Clear();
            Index(root);
            view.SetTree(root);
        });
    }

    /// <summary>Fetches the properties of an Element and shows them.</summary>
    /// <param name="id">The id of the Element.</param>
    /// <returns>A task that completes when the view has been updated.</returns>
    public async Task SelectAsync(int id)
    {
        var properties = InspectorProperty.ParseAll(await client.CallAsync("element.cascade", $$"""{"id":{{id}}}"""));

        post(() => view.SetProperties(id, nodes.TryGetValue(id, out var node) ? node.Label : $"#{id}", properties));
    }

    /// <summary>Remembers a node and its descendants.</summary>
    /// <param name="node">The node.</param>
    void Index(InspectorNode node)
    {
        nodes[node.Id] = node;

        foreach (var child in node.Children)
            Index(child);
    }
}
