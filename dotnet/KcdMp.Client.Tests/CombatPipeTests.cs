using System.Diagnostics;

namespace KcdMp.Client.Tests;

public class CombatPipeTests
{
    // The native plugin cannot exist on Game Pass. Looking for its pipe costs
    // 500 ms each time, inside the loop that applies every other packet.
    [Fact]
    public async Task A_disabled_pipe_answers_at_once_and_applies_nothing()
    {
        await using var pipe = new CombatPipe { Disabled = true };
        var sw = Stopwatch.StartNew();

        Assert.False(await pipe.EnsureConnectedAsync());
        Assert.False(await pipe.ApplyDeathAsync(Guid.NewGuid()));
        Assert.False(await pipe.ApplyDamageAsync(Guid.NewGuid(), 1f, 1f, false));
        Assert.False(await pipe.PingAsync());

        Assert.True(sw.ElapsedMilliseconds < 200, $"took {sw.ElapsedMilliseconds} ms");
    }

    // A Steam machine whose plugin is not injected (the dedicated host, or a
    // failed injection): one 500 ms look, then no more for a while, instead of
    // 500 ms for every combat packet.
    [Fact]
    public async Task A_pipe_that_is_not_there_is_not_looked_for_again_at_once()
    {
        await using var pipe = new CombatPipe();
        Assert.False(await pipe.EnsureConnectedAsync());

        var sw = Stopwatch.StartNew();
        Assert.False(await pipe.EnsureConnectedAsync());
        Assert.False(await pipe.ApplyDeathAsync(Guid.NewGuid()));
        Assert.True(sw.ElapsedMilliseconds < 200, $"took {sw.ElapsedMilliseconds} ms");
    }
}
