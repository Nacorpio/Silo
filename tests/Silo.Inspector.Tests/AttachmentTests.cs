using SDL;
using Silo;
using Silo.Inspector;
using Silo.Platform.Sdl;
using Xunit;

/// <summary>Runs an app window and its Inspector window on the dummy video driver.</summary>
public class AttachmentTests
{
    [Fact]
    public async Task Opening_the_inspector_shows_the_apps_tree_in_a_second_window()
    {
        SDL3.SDL_SetHint("SDL_VIDEO_DRIVER", "dummy");

        var root = new Box { Name = "root" };

        root.Children.Add(new Button { Name = "save" });

        using var app = new SdlWindow("app", 200, 100, root);
        using var attachment = app.AttachDiagnostics($"silo-test-{Guid.NewGuid():N}");

        var inspector = attachment.OpenInspector();
        var view = (InspectorView)inspector.Root;
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (view.RowCount == 0 && DateTime.UtcNow < deadline)
        {
            app.Pump(20);

            await Task.Yield();
        }

        Assert.Equal(2, view.RowCount);
        Assert.Same(inspector, attachment.OpenInspector());
    }

    [Fact]
    public void Disposing_the_attachment_closes_the_inspector_window()
    {
        SDL3.SDL_SetHint("SDL_VIDEO_DRIVER", "dummy");

        using var app = new SdlWindow("app", 200, 100, new Box());

        var attachment = app.AttachDiagnostics($"silo-test-{Guid.NewGuid():N}");

        attachment.OpenInspector();
        attachment.Dispose();

        Assert.Null(attachment.InspectorWindow);
    }
}
