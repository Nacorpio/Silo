using Silo;
using Silo.Diagnostics;
using Silo.Styling;
using Xunit;

public class DiagnosticsServerTests
{
    static (DiagnosticsServer Server, Box Root) Start(Func<Func<string?>, Task<string?>>? invoke = null)
    {
        var root = new Box { Name = "root" };
        var agent = new DiagnosticsAgent(root, () => null);
        var server = new DiagnosticsServer(agent, invoke ?? (work => Task.FromResult(work())), $"silo-test-{Guid.NewGuid():N}");

        root.Children.Add(new Button { Text = "OK" });
        server.Start();

        return (server, root);
    }

    [Fact]
    public async Task A_client_can_call_the_agent_over_the_pipe()
    {
        var (server, _) = Start();

        using (server)
        {
            await using var client = await DiagnosticsClient.ConnectAsync(server.PipeName);

            var hello = await client.CallAsync("hello");
            var tree = await client.CallAsync("tree.snapshot");

            Assert.Equal(DiagnosticsAgent.ProtocolVersion, hello.GetProperty("protocol").GetInt32());
            Assert.Equal("Box", tree.GetProperty("type").GetString());
            Assert.Equal("Button", tree.GetProperty("children")[0].GetProperty("type").GetString());
        }
    }

    [Fact]
    public async Task Every_request_goes_through_the_invoke_function()
    {
        var calls = 0;

        var (server, _) = Start(work =>
        {
            calls++;

            return Task.FromResult(work());
        });

        using (server)
        {
            await using var client = await DiagnosticsClient.ConnectAsync(server.PipeName);

            await client.CallAsync("hello");
            await client.CallAsync("hello");

            Assert.Equal(2, calls);
        }
    }

    [Fact]
    public async Task An_error_response_becomes_an_exception_in_the_client()
    {
        var (server, _) = Start();

        using (server)
        {
            await using var client = await DiagnosticsClient.ConnectAsync(server.PipeName);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.CallAsync("nope"));

            Assert.Contains("nope", error.Message);
        }
    }

    [Fact]
    public async Task Several_clients_can_connect_one_after_another()
    {
        var (server, _) = Start();

        using (server)
        {
            for (var i = 0; i < 3; i++)
            {
                await using var client = await DiagnosticsClient.ConnectAsync(server.PipeName);

                Assert.Equal(1, (await client.CallAsync("hello")).GetProperty("protocol").GetInt32());
            }
        }
    }
}
