namespace KcdMp.Client;

/// <summary>
/// The outbound half of talking to the game: running Lua. Split out of
/// <see cref="IGameTransport"/> so the log-tail transport can send through
/// either the debug HTTP API (Modding Tools build) or RemoteConsole (Game Pass).
/// </summary>
public interface ILuaCommandSink
{
    /// <summary>
    /// Runs a Lua statement in the game. Fire-and-forget: no value comes back.
    /// A batching transport may buffer this until <see cref="FlushAsync"/>.
    /// </summary>
    Task ExecuteAsync(string lua, CancellationToken ct = default);

    /// <summary>
    /// Sends anything buffered by <see cref="ExecuteAsync"/>. A no-op for
    /// transports that send immediately, so callers can always call it.
    /// </summary>
    Task FlushAsync(CancellationToken ct = default);

    /// <summary>
    /// Runs a Lua statement immediately, bypassing the batch buffer that
    /// <see cref="ExecuteAsync"/> writes into.
    ///
    /// Exists for WO-13's interp pump, where the timing *is* the feature: the
    /// batch is flushed by the agent's position loop, so a batched pump frame
    /// would arrive a whole tick late, every tick. Use <see cref="ExecuteAsync"/>
    /// for everything else -- one HTTP round trip per statement is exactly the
    /// cost batching exists to avoid.
    ///
    /// This replaced WO-11's <c>SetTimeScaleAsync</c>, whose only caller was
    /// the peer-slowdown response retired in WO-13. The underlying capability
    /// is still real and documented in docs/WO-11-findings.md s0.4 if anything
    /// ever needs it again; it just has no caller.
    /// </summary>
    Task ExecuteNowAsync(string lua, CancellationToken ct = default);
}
