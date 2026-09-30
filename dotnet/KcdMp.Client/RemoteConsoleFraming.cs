using System.Text;

namespace KcdMp.Client;

/// <summary>
/// Wire framing for CryEngine's RemoteConsole, which KCD2 opens on :4600 when
/// started with -devmode: one message is an ASCII type digit, a UTF-8 payload
/// and a NUL. Type '5' is a console command, and a '#' prefix routes the
/// command to the Lua VM. Pure, so the size rules are testable without a game.
/// </summary>
public static class RemoteConsoleFraming
{
    /// <summary>
    /// Largest frame sent, type byte and NUL included. The engine receives into
    /// 4096-byte buffers; this stays well clear of that.
    /// </summary>
    public const int DefaultMaxFrameBytes = 3800;

    private const byte ConsoleCommand = (byte)'5';
    private const int LuaFrameOverhead = 3; // '5' + '#' + NUL

    /// <summary>Frames one console command. A NUL would end the frame early, so it is refused.</summary>
    public static byte[] Frame(string command)
    {
        if (command.Contains('\0'))
            throw new ArgumentException("A console command cannot contain NUL.", nameof(command));
        var frame = new byte[Encoding.UTF8.GetByteCount(command) + 2];
        frame[0] = ConsoleCommand;
        Encoding.UTF8.GetBytes(command, 0, command.Length, frame, 1);
        return frame;
    }

    /// <summary>
    /// Wraps each statement in its own pcall -- as the HTTP path does, so one
    /// failing statement cannot swallow the rest -- and packs them into as few
    /// '#' frames as fit in <paramref name="maxFrameBytes"/>. A statement is
    /// never split across frames: one that cannot fit alone, or that contains
    /// NUL, is left out and counted in <paramref name="dropped"/>.
    /// </summary>
    public static List<byte[]> BuildLuaFrames(IEnumerable<string> statements, int maxFrameBytes, out int dropped)
    {
        dropped = 0;
        var frames = new List<byte[]>();
        var sb = new StringBuilder();
        int bytes = 0;
        foreach (var stmt in statements)
        {
            if (stmt.Contains('\0')) { dropped++; continue; }
            string piece = $"pcall(function() {stmt} end)\n";
            int pieceBytes = Encoding.UTF8.GetByteCount(piece);
            if (LuaFrameOverhead + pieceBytes > maxFrameBytes) { dropped++; continue; }
            if (bytes > 0 && LuaFrameOverhead + bytes + pieceBytes > maxFrameBytes)
            {
                frames.Add(Frame("#" + sb));
                sb.Clear();
                bytes = 0;
            }
            sb.Append(piece);
            bytes += pieceBytes;
        }
        if (bytes > 0) frames.Add(Frame("#" + sb));
        return frames;
    }
}
