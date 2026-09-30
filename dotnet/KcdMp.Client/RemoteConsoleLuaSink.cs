using System.Text;
using System.Net.Sockets;
using System.Threading.Channels;

namespace KcdMp.Client;

/// <summary>
/// Runs Lua in the game over CryEngine's RemoteConsole -- the Game Pass
/// build's only way in: it has no debug HTTP API, but with -devmode it listens
/// on :4600 and runs '#'-prefixed console commands as Lua.
///
/// The game drives the conversation. It sends one frame and waits for exactly
/// one reply (measured live on Game Pass 1.5.6; matches CryEngine's
/// SRemoteClient loop):
///
///   '1' request              -> one console command ('5'), or no-op ('0')
///   '6' / '7' autocomplete   -> no-op
///   '2' / '3' / '4' log line -> MUST be a no-op: any other reply and the
///                               game never reads this connection again
///
/// Replying to a log line with a command was the live failure: the connection
/// died every 22 frames, each death cost a 2 s stall, and ghosts froze for
/// half of every session. A client that follows the table ran 450 of 450
/// commands in order with no stall.
///
/// So one pump task owns the socket and answers every frame the game sends;
/// callers only queue. Nothing here blocks the agent's position or relay
/// loops on the game, and a send never throws. Frames wait in a bounded
/// queue; one that has waited too long (the game was away) is dropped rather
/// than replayed stale.
/// </summary>
public sealed class RemoteConsoleLuaSink(
    string host = "127.0.0.1",
    int port = 4600,
    int maxFrameBytes = RemoteConsoleFraming.DefaultMaxFrameBytes,
    int idleHoldMs = 15,
    int staleAfterMs = 5000,
    int maxFrameAgeMs = 5000) : ILuaCommandSink, IAsyncDisposable
{
    private const byte Request = (byte)'1';
    private const int ConnectTimeoutMs = 1000;
    private const int ReconnectDelayMs = 250;
    private const int MaxQueuedFrames = 512;
    private static readonly byte[] NoOp = [(byte)'0', 0];

    private readonly record struct Queued(byte[] Frame, long AtMs);

    private readonly List<string> _pending = [];
    private readonly object _pendingLock = new();
    private readonly Channel<Queued> _frames = Channel.CreateBounded<Queued>(
        new BoundedChannelOptions(MaxQueuedFrames) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
    private readonly CancellationTokenSource _cts = new();
    private readonly object _pumpLock = new();
    private Task? _pump;
    private volatile bool _connected;
    private long _framesSent, _framesExpired, _statementsDropped, _connections;
    private int _dropLogged;

    public string Endpoint => $"{host}:{port}";
    public bool IsConnected => _connected;
    public long FramesSent => Interlocked.Read(ref _framesSent);

    /// <summary>Frames dropped because they waited longer than the age limit.</summary>
    public long FramesExpired => Interlocked.Read(ref _framesExpired);
    public long StatementsDropped => Interlocked.Read(ref _statementsDropped);

    /// <summary>Connections opened since start. More than one means the game went away and came back.</summary>
    public long Connections => Interlocked.Read(ref _connections);

    public Task ExecuteAsync(string lua, CancellationToken ct = default)
    {
        bool flushNow;
        lock (_pendingLock)
        {
            _pending.Add(lua);
            flushNow = _pending.Sum(s => Encoding.UTF8.GetByteCount(s) + 32) >= maxFrameBytes;
        }
        return flushNow ? FlushAsync(ct) : Task.CompletedTask;
    }

    public Task FlushAsync(CancellationToken ct = default)
    {
        string[] batch;
        lock (_pendingLock)
        {
            if (_pending.Count == 0) return Task.CompletedTask;
            batch = [.. _pending];
            _pending.Clear();
        }
        Enqueue(batch);
        return Task.CompletedTask;
    }

    public Task ExecuteNowAsync(string lua, CancellationToken ct = default)
    {
        Enqueue([lua]);
        return Task.CompletedTask;
    }

    private void Enqueue(IEnumerable<string> statements)
    {
        var frames = RemoteConsoleFraming.BuildLuaFrames(statements, maxFrameBytes, out int dropped);
        if (dropped > 0)
        {
            Interlocked.Add(ref _statementsDropped, dropped);
            if (Interlocked.Exchange(ref _dropLogged, 1) == 0)
                Console.WriteLine($"[rc] dropped {dropped} Lua statement(s) too large for one RemoteConsole frame ({maxFrameBytes} bytes)");
        }

        long now = Environment.TickCount64;
        foreach (var f in frames) _frames.Writer.TryWrite(new Queued(f, now));

        if (_pump is null)
        {
            lock (_pumpLock) _pump ??= Task.Run(PumpAsync);
        }
    }

    private async Task PumpAsync()
    {
        var ct = _cts.Token;
        while (!ct.IsCancellationRequested)
        {
            using var client = new TcpClient { NoDelay = true };
            try
            {
                using (var connect = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    connect.CancelAfter(ConnectTimeoutMs);
                    await client.ConnectAsync(host, port, connect.Token);
                }
                Interlocked.Increment(ref _connections);
                _connected = true;
                Console.WriteLine($"[rc] connected to the game console at {Endpoint}");
                await ServeAsync(client.GetStream(), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch { /* not listening yet, reset, or gone quiet: reconnect below */ }

            if (_connected)
            {
                _connected = false;
                if (!ct.IsCancellationRequested)
                    Console.WriteLine("[rc] lost the game console; reconnecting");
            }

            try { await Task.Delay(ReconnectDelayMs, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>
    /// Answers every frame the game sends until the connection ends. Returns
    /// on end-of-stream; throws when the game sends nothing for
    /// <c>staleAfterMs</c> or the socket fails -- either way the pump reconnects.
    /// </summary>
    private async Task ServeAsync(NetworkStream stream, CancellationToken ct)
    {
        var buf = new byte[8192];
        int len = 0;
        while (true)
        {
            using var turn = CancellationTokenSource.CreateLinkedTokenSource(ct);
            turn.CancelAfter(staleAfterMs);

            int n = await stream.ReadAsync(buf.AsMemory(len), turn.Token);
            if (n == 0) return;
            len += n;

            int start = 0;
            for (int i = start; i < len; i++)
            {
                if (buf[i] != 0) continue;
                await ReplyAsync(stream, buf[start], turn.Token, ct);
                start = i + 1;
            }

            if (start == 0 && len == buf.Length)
                throw new InvalidDataException("RemoteConsole frame larger than the receive buffer.");
            if (start > 0)
            {
                Buffer.BlockCopy(buf, start, buf, 0, len - start);
                len -= start;
            }
        }
    }

    private async Task ReplyAsync(NetworkStream stream, byte frameType, CancellationToken turn, CancellationToken ct)
    {
        byte[]? frame = frameType == Request ? await TakeFrameAsync(ct) : null;
        await stream.WriteAsync(frame ?? NoOp, turn);
        if (frame is not null) Interlocked.Increment(ref _framesSent);
    }

    /// <summary>
    /// The next queued frame, waiting up to <c>idleHoldMs</c> for one to
    /// arrive. Holding the game's request briefly is what paces the exchange:
    /// answering "nothing" instantly would spin both sides at socket speed,
    /// while a command queued during the hold goes out at once.
    /// </summary>
    private async Task<byte[]?> TakeFrameAsync(CancellationToken ct)
    {
        if (TryTakeFresh(out var ready)) return ready;

        using var hold = CancellationTokenSource.CreateLinkedTokenSource(ct);
        hold.CancelAfter(idleHoldMs);
        try
        {
            while (await _frames.Reader.WaitToReadAsync(hold.Token))
            {
                if (TryTakeFresh(out var frame)) return frame;
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
        return null;
    }

    private bool TryTakeFresh(out byte[]? frame)
    {
        long oldest = Environment.TickCount64 - maxFrameAgeMs;
        while (_frames.Reader.TryRead(out var q))
        {
            if (q.AtMs >= oldest) { frame = q.Frame; return true; }
            Interlocked.Increment(ref _framesExpired);
        }
        frame = null;
        return false;
    }

    public async ValueTask DisposeAsync()
    {
        await FlushAsync();

        // Best effort: give already-queued frames (the emitter stop, ghost
        // cleanup) a moment to go out before the pump is torn down.
        var deadline = Environment.TickCount64 + 300;
        while (_connected && _frames.Reader.Count > 0 && Environment.TickCount64 < deadline)
            await Task.Delay(10);

        _cts.Cancel();
        if (_pump is not null)
        {
            try { await _pump; } catch { }
        }
        _cts.Dispose();
    }
}
