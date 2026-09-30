using System.Text;
using System.Text.RegularExpressions;

namespace KcdMp.Client.Tests;

public class LogTailGameTransportTests : IDisposable
{
    private readonly string _log = Path.Combine(Path.GetTempPath(), $"kcd-test-{Guid.NewGuid():N}.log");

    public LogTailGameTransportTests() => File.WriteAllText(_log, "engine boot");
    public void Dispose() { try { File.Delete(_log); } catch { } }

    /// <summary>
    /// Writes a line the way the game does (measured on Game Pass 1.5.6: the
    /// last byte of kcd.log was never a newline in 300 samples): the newline
    /// goes in front, so a line is only terminated by the one after it.
    /// </summary>
    private void Append(string line)
    {
        using var fs = new FileStream(_log, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        fs.Write(Encoding.UTF8.GetBytes("\r\n" + line));
    }

    /// <summary>Runs the probe's System.LogAlways calls like the game would: one log line each.</summary>
    private void RunProbe(string lua, bool playerExists)
    {
        foreach (Match m in Regex.Matches(lua, @"System\.LogAlways\(""([^""]*)""(\.\.tostring\(player ~= nil\))?\)"))
            Append(m.Groups[1].Value + (m.Groups[2].Success ? (playerExists ? "true" : "false") : ""));
    }

    /// <summary>Plays the game: what it does with an immediate command is up to the test.</summary>
    private sealed class FakeGame(Action<string> onNow) : ILuaCommandSink
    {
        public List<string> Sent { get; } = [];
        public Task ExecuteAsync(string lua, CancellationToken ct = default) { Sent.Add(lua); return Task.CompletedTask; }
        public Task FlushAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task ExecuteNowAsync(string lua, CancellationToken ct = default) { Sent.Add(lua); onNow(lua); return Task.CompletedTask; }
    }

    private static string NonceOf(string probe) =>
        Regex.Match(probe, @"\[KCD2-MP-RDY\] ([0-9a-f]+) ").Groups[1].Value;

    private LogTailGameTransport Tail(ILuaCommandSink sink, int timeoutMs = 1000) =>
        new(sink, null, _log) { ReadyProbeTimeout = TimeSpan.FromMilliseconds(timeoutMs) };

    [Fact]
    public async Task Ready_when_the_game_reports_a_player()
    {
        // The log is otherwise silent here -- no emitter yet -- so nothing but
        // the probe itself can terminate the probe's reply line. This is the
        // live failure: the game answered "true" and the agent never saw it.
        await using var tail = Tail(new FakeGame(lua => RunProbe(lua, playerExists: true)));
        Assert.True(await tail.IsGameReadyAsync());
    }

    [Fact]
    public async Task Not_ready_at_main_menu_answers_at_once()
    {
        await using var tail = Tail(new FakeGame(lua => RunProbe(lua, playerExists: false)), timeoutMs: 2000);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Assert.False(await tail.IsGameReadyAsync());
        Assert.True(sw.ElapsedMilliseconds < 1000, $"a 'false' answer should not wait for the timeout, took {sw.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task Stale_or_echoed_lines_do_not_count()
    {
        await using var tail = Tail(new FakeGame(lua =>
        {
            Append("[KCD2-MP-RDY] deadbeef0000 true");                               // someone else's probe
            Append($"[KCD2-MP-RDY] {NonceOf(lua)} \"..tostring(player ~= nil))");    // the command echoed back
            Append("some later engine line");
        }), timeoutMs: 400);
        Assert.False(await tail.IsGameReadyAsync());
    }

    [Fact]
    public async Task No_answer_times_out_false()
    {
        await using var tail = Tail(new FakeGame(_ => { }), timeoutMs: 300);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Assert.False(await tail.IsGameReadyAsync());
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), $"took {sw.Elapsed}");
    }

    [Fact]
    public async Task Cancelled_probe_reads_as_not_ready()
    {
        await using var tail = Tail(new FakeGame(_ => { }), timeoutMs: 5000);
        using var cts = new CancellationTokenSource(150);
        Assert.False(await tail.IsGameReadyAsync(cts.Token));
    }

    [Fact]
    public async Task Reflection_members_degrade_to_null_without_the_debug_api()
    {
        await using var tail = Tail(new FakeGame(_ => { }));
        Assert.False(tail.HasReflection);
        Assert.Null(await tail.ReadEquippedItemClassesAsync());
        Assert.Null(await tail.ReadGhostEquippedItemClassesAsync("kcd2mp_1"));
        Assert.Null(await tail.ReadGhostSoulGuidAsync("kcd2mp_1"));
        Assert.Null(await tail.ReadSoulNameByGuidAsync(Guid.NewGuid()));
        await tail.EquipItemOnGhostAsync("kcd2mp_1", Guid.NewGuid(), createIfMissing: true);
        await tail.UnequipItemOnGhostAsync("kcd2mp_1", Guid.NewGuid());
    }

    [Fact]
    public async Task Start_asks_the_mod_to_emit_through_the_sink_and_reads_game_pass_lines()
    {
        var game = new FakeGame(_ => { });
        await using var tail = Tail(game);
        await tail.StartAsync();
        Assert.Contains("KCD2MP_StartEmitter(20)", game.Sent);

        // Verbatim from the Game Pass spike's kcd.log; the second line is what
        // terminates the first.
        await Task.Delay(200);
        Append("[KCD2-MP-DATA] v2 1 194.683 754.746 3348.794 141.960 -1.2165 0 100.00 126.67");
        Append("[KCD2-MP-DATA] v2 2 194.717 754.746 3348.794 141.960 -1.2165 0 100.00 126.67");
        PlayerState? s = null;
        var until = DateTime.UtcNow.AddSeconds(3);
        while (s is null && DateTime.UtcNow < until) { s = await tail.ReadPlayerStateAsync(); await Task.Delay(10); }
        Assert.NotNull(s);
        Assert.Equal(754.746, s!.Value.X, 3);
        Assert.Equal(3348.794, s.Value.Y, 3);
        Assert.Equal(100f, s.Value.Health!.Value);
    }
}
