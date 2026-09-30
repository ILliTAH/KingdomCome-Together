using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace KcdMp.Client.Tests;

/// <summary>
/// Stands in for the game's RemoteConsole and enforces the rules the real one
/// does (measured on Game Pass 1.5.6): it sends one frame and waits for one
/// reply; it runs only the first message of what it read; a command is
/// accepted in reply to a request or an autocomplete entry; and a reply to a
/// log line that is not a no-op makes it abandon the connection -- it stops
/// reading and never closes, exactly like the game.
/// </summary>
internal sealed class FakeRemoteConsole : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<TcpClient> _clients = [];
    private readonly ConcurrentQueue<string> _logLines = new();
    private int _generation;
    private int _mutedUpTo = -1;

    public ConcurrentQueue<string> Commands { get; } = new();
    public int Port { get; }

    /// <summary>Connections abandoned because a log line was answered with something other than a no-op.</summary>
    public int Violations;

    /// <summary>Messages lost because they arrived behind another one in the same read.</summary>
    public int MergedAway;

    /// <param name="logEveryMs">When set, a log line is queued for the client at this interval, like a busy kcd.log.</param>
    public FakeRemoteConsole(int port = 0, int logEveryMs = 0)
    {
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _ = AcceptLoopAsync();
        if (logEveryMs > 0) _ = LogLoopAsync(logEveryMs);
    }

    /// <summary>Every connection open right now goes silent; new ones behave.</summary>
    public void MuteExistingClients() => Volatile.Write(ref _mutedUpTo, Volatile.Read(ref _generation));

    private async Task LogLoopAsync(int everyMs)
    {
        try
        {
            for (int i = 0; ; i++)
            {
                if (_logLines.Count < 1000) _logLines.Enqueue($"[KCD2-MP-DATA] v2 {i} 1.0 2.0 3.0 4.0 0.0 0 100.00 100.00");
                await Task.Delay(everyMs, _cts.Token);
            }
        }
        catch { }
    }

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (true)
            {
                var c = await _listener.AcceptTcpClientAsync(_cts.Token);
                lock (_clients) _clients.Add(c);
                _ = ServeAsync(c, Interlocked.Increment(ref _generation));
            }
        }
        catch { }
    }

    private async Task ServeAsync(TcpClient c, int generation)
    {
        var s = c.GetStream();
        int autoComplete = 45;
        bool autoCompleteDone = false;
        try
        {
            while (true)
            {
                if (!await ExchangeAsync(s, generation, "1", commandAllowed: true)) break;
                for (int i = 0; i < 20 && autoComplete > 0; i++, autoComplete--)
                    if (!await ExchangeAsync(s, generation, "6wh_some_cvar", commandAllowed: true)) goto abandoned;
                if (autoComplete == 0 && !autoCompleteDone)
                {
                    autoCompleteDone = true;
                    if (!await ExchangeAsync(s, generation, "7", commandAllowed: true)) break;
                }
                while (_logLines.TryDequeue(out var line))
                    if (!await ExchangeAsync(s, generation, "2" + line, commandAllowed: false)) goto abandoned;
            }
        abandoned:
            await Task.Delay(Timeout.Infinite, _cts.Token); // stops reading, never closes
        }
        catch { }
    }

    /// <summary>Sends one frame and reads one reply. False when the connection is abandoned.</summary>
    private async Task<bool> ExchangeAsync(NetworkStream s, int generation, string frame, bool commandAllowed)
    {
        if (generation <= Volatile.Read(ref _mutedUpTo)) return false;
        await s.WriteAsync(Encoding.UTF8.GetBytes(frame + "\0"), _cts.Token);

        // Like the engine's RecvPackage: read until the data ends in a NUL,
        // then look only at the first message in it.
        var buf = new byte[4096];
        int len = 0;
        do
        {
            int n = await s.ReadAsync(buf.AsMemory(len), _cts.Token);
            if (n == 0) throw new IOException("client closed");
            len += n;
        } while (buf[len - 1] != 0);
        if (generation <= Volatile.Read(ref _mutedUpTo)) return false;

        int nul = Array.IndexOf(buf, (byte)0, 0, len);
        if (nul != len - 1) Interlocked.Increment(ref MergedAway);

        if (buf[0] == (byte)'0') return true;
        if (buf[0] == (byte)'5' && commandAllowed)
        {
            Commands.Enqueue(Encoding.UTF8.GetString(buf, 1, nul - 1));
            return true;
        }
        Interlocked.Increment(ref Violations);
        return false;
    }

    public async Task<string> NextCommandAsync(int timeoutMs = 3000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < until)
        {
            if (Commands.TryDequeue(out var cmd)) return cmd;
            await Task.Delay(5);
        }
        throw new TimeoutException("the fake console received no command");
    }

    public ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _listener.Stop();
        lock (_clients) { foreach (var c in _clients) c.Dispose(); _clients.Clear(); }
        return ValueTask.CompletedTask;
    }
}
