using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace KcdMp.Client.Tests;

public class RemoteConsoleLuaSinkTests
{
    private static async Task WaitUntil(Func<bool> cond, int timeoutMs = 3000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!cond() && DateTime.UtcNow < until) await Task.Delay(10);
        Assert.True(cond());
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    [Fact]
    public async Task Flush_sends_batched_statements_as_hash_lua()
    {
        await using var game = new FakeRemoteConsole();
        await using var sink = new RemoteConsoleLuaSink("127.0.0.1", game.Port);
        await sink.ExecuteAsync("a()");
        await sink.ExecuteAsync("b()");
        await sink.FlushAsync();
        Assert.Equal("#pcall(function() a() end)\npcall(function() b() end)\n", await game.NextCommandAsync());
    }

    [Fact]
    public async Task ExecuteNow_sends_without_a_flush()
    {
        await using var game = new FakeRemoteConsole();
        await using var sink = new RemoteConsoleLuaSink("127.0.0.1", game.Port);
        await sink.ExecuteNowAsync("KCD2MP_InterpPump()");
        Assert.Equal("#pcall(function() KCD2MP_InterpPump() end)\n", await game.NextCommandAsync());
    }

    [Fact]
    public async Task Commands_survive_a_busy_game_log()
    {
        // The live failure: with log lines flowing, the old sender answered one
        // with a command every 22 frames and the game abandoned the connection.
        await using var game = new FakeRemoteConsole(logEveryMs: 1);
        await using var sink = new RemoteConsoleLuaSink("127.0.0.1", game.Port, maxFrameBytes: 200);
        for (int i = 0; i < 150; i++)
        {
            await sink.ExecuteNowAsync($"u({i})");
            if (i % 10 == 0) await Task.Delay(15);
        }

        var seen = new List<string>();
        while (seen.Count < 150)
            seen.AddRange((await game.NextCommandAsync()).TrimStart('#').Split('\n', StringSplitOptions.RemoveEmptyEntries));

        Assert.Equal(Enumerable.Range(0, 150).Select(i => $"pcall(function() u({i}) end)"), seen);
        Assert.Equal(0, game.Violations);
        Assert.Equal(0, game.MergedAway);
        Assert.Equal(1, sink.Connections);
    }

    [Fact]
    public async Task Callers_never_wait_on_the_game()
    {
        await using var sink = new RemoteConsoleLuaSink("127.0.0.1", FreePort());
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            await sink.ExecuteAsync($"u({i})");
            await sink.FlushAsync();
            await sink.ExecuteNowAsync("now()");
        }
        Assert.True(sw.Elapsed < TimeSpan.FromMilliseconds(500), $"took {sw.Elapsed}");
        Assert.False(sink.IsConnected);
    }

    [Fact]
    public async Task Stale_frames_are_dropped_not_replayed()
    {
        int port = FreePort();
        await using var sink = new RemoteConsoleLuaSink("127.0.0.1", port, maxFrameAgeMs: 200);
        await sink.ExecuteNowAsync("old()");       // the game is not running yet
        await Task.Delay(400);

        await using var game = new FakeRemoteConsole(port);
        await WaitUntil(() => sink.IsConnected);
        await sink.ExecuteNowAsync("fresh()");

        Assert.Contains("fresh()", await game.NextCommandAsync());
        Assert.Empty(game.Commands);
        Assert.Equal(1, sink.FramesExpired);
    }

    [Fact]
    public async Task Silent_console_is_replaced_by_a_fresh_connection()
    {
        await using var game = new FakeRemoteConsole();
        await using var sink = new RemoteConsoleLuaSink("127.0.0.1", game.Port, staleAfterMs: 300);
        await sink.ExecuteNowAsync("a()");
        Assert.Contains("a()", await game.NextCommandAsync());

        game.MuteExistingClients();                // the game stops talking on this connection
        await Task.Delay(100);
        await sink.ExecuteNowAsync("b()");         // waits in the queue; goes out on the next connection
        Assert.Contains("b()", await game.NextCommandAsync());
        Assert.Equal(2, sink.Connections);
    }

    [Fact]
    public async Task Reconnects_after_the_game_restarts()
    {
        var game = new FakeRemoteConsole();
        int port = game.Port;
        await using var sink = new RemoteConsoleLuaSink("127.0.0.1", port);
        await sink.ExecuteNowAsync("first()");
        Assert.Contains("first()", await game.NextCommandAsync());

        await game.DisposeAsync();                 // the game quits
        await WaitUntil(() => !sink.IsConnected);

        await using var restarted = new FakeRemoteConsole(port);
        await WaitUntil(() => sink.IsConnected);
        await sink.ExecuteNowAsync("second()");
        Assert.Contains("second()", await restarted.NextCommandAsync());
    }
}
