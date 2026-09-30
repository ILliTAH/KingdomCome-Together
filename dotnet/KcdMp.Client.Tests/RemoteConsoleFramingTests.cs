using System.Text;

namespace KcdMp.Client.Tests;

public class RemoteConsoleFramingTests
{
    private static string Payload(byte[] frame)
    {
        Assert.Equal((byte)'5', frame[0]);
        Assert.Equal(0, frame[^1]);
        return Encoding.UTF8.GetString(frame, 1, frame.Length - 2);
    }

    [Fact]
    public void Frame_is_type5_utf8_nul() =>
        Assert.Equal(new byte[] { (byte)'5', (byte)'#', (byte)'x', 0 }, RemoteConsoleFraming.Frame("#x"));

    [Fact]
    public void Frame_rejects_nul() =>
        Assert.Throws<ArgumentException>(() => RemoteConsoleFraming.Frame("a\0b"));

    [Fact]
    public void Small_batch_is_one_frame_with_each_statement_in_its_own_pcall()
    {
        var frames = RemoteConsoleFraming.BuildLuaFrames(["a()", "b()"], 3800, out int dropped);
        Assert.Equal(0, dropped);
        Assert.Equal("#pcall(function() a() end)\npcall(function() b() end)\n", Payload(Assert.Single(frames)));
    }

    [Fact]
    public void Large_batch_splits_without_splitting_statements()
    {
        var stmts = Enumerable.Range(0, 200).Select(i => $"KCD2MP_Ghost({i}, \"{new string('x', 40)}\")").ToList();
        var frames = RemoteConsoleFraming.BuildLuaFrames(stmts, 1000, out int dropped);
        Assert.Equal(0, dropped);
        Assert.True(frames.Count > 1);
        Assert.All(frames, f => Assert.True(f.Length <= 1000));
        var seen = frames.SelectMany(f => Payload(f).TrimStart('#').Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal(stmts.Select(s => $"pcall(function() {s} end)"), seen);
    }

    [Fact]
    public void Limit_counts_utf8_bytes_not_chars()
    {
        // 400 Thai characters: 400 UTF-16 chars, 1200 UTF-8 bytes.
        var frames = RemoteConsoleFraming.BuildLuaFrames([$"KCD2MP_Name(\"{new string('ก', 400)}\")"], 1000, out int dropped);
        Assert.Empty(frames);
        Assert.Equal(1, dropped);
    }

    [Fact]
    public void Oversized_or_nul_statements_are_dropped_neighbours_kept()
    {
        var frames = RemoteConsoleFraming.BuildLuaFrames(["a()", new string('y', 5000), "b(\0)", "c()"], 1000, out int dropped);
        Assert.Equal(2, dropped);
        Assert.Equal("#pcall(function() a() end)\npcall(function() c() end)\n", Payload(Assert.Single(frames)));
    }

    [Fact]
    public void Thai_text_round_trips_intact()
    {
        var frames = RemoteConsoleFraming.BuildLuaFrames(["KCD2MP_Name(\"สมชาย\")"], 3800, out _);
        Assert.Contains("สมชาย", Payload(Assert.Single(frames)));
    }
}
