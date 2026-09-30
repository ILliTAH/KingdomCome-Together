# What this fork changes from Kingdom Come: Together 0.18.2

Base: tag `0.18.2` of DeepFriedDepp/KingdomCome-Together (GPLv3). `VERSION`
stays `0.18.2` and the wire protocol is untouched, so a stock 0.18.2 relay and
stock Steam clients accept these builds. Work done 2026-09-30 on a Game Pass
machine (game v1.5.6.0); "live" below means measured on that game.

## Agent (`dotnet/KcdMp.Client`)

| Change | Why (evidence) |
|---|---|
| `ILuaCommandSink`, `RemoteConsoleFraming`, `RemoteConsoleLuaSink`; `--transport remoteconsole` | The Game Pass build has no `:1403` debug API. With `-devmode` it opens CryEngine's RemoteConsole on `:4600` and runs `#`-prefixed commands as Lua (live). |
| The sink answers every frame the game sends; commands go out only in reply to a request frame, everything else gets a no-op; callers only queue | Live: replying to a log-line frame (`'2'`) with a command makes the game abandon the connection. The first sender did that every 22 frames: 276 two-second stalls in 19 minutes, ghosts frozen half the time. A conformant client ran 450 of 450 commands in order with no stall; the fixed agent ran a real session with 0 stalls and 0 reconnects. |
| Frames capped at 3800 bytes, never split inside a statement | Live: 4095-byte frames run whole, 6000 and 9000 do not. |
| Readiness by a nonce probe read back from `kcd.log`; the probe logs a second line | No `:1403` to ask. Live: the game writes the newline in front of each log line, so the reply stayed unterminated until another line came and the agent never saw "true". |
| `LogTailGameTransport` takes any sink and an optional reflection transport | Appearance and soul reads exist only on `:1403`; without it they answer "unknown", which callers already skip. |
| `KcdLogLocator.FindGamePass()` | Game Pass writes `kcd.log` to Documents. The Steam search is unchanged. |
| Swing events skip the DLL pipe in RemoteConsole mode | No DLL on Game Pass: the pipe attempt cost a 500 ms timeout per swing and froze the ghost 0.9 s. |
| Session weather re-applied (snap) after a save reload | A reload restores the save's weather and the change gate kept the session profile off for up to 20 minutes. |

Tests: `dotnet test dotnet/KcdMp.Client.Tests` (25), including a fake
RemoteConsole that enforces the rules the real one does.

## Mod (`kdcmp/Data/Scripts/Startup/kdcmp.lua`) — search `Game Pass fork` and `Host world`

| Change | Why (evidence) |
|---|---|
| Ghost fallback spawns `NPC_NAI` instead of `NPC` when `XGenAIModule.SpawnEntity` is missing | Live: a plain `NPC` carries a brain — it walked 17 m off by itself in 18 s, fought the position stream and attacked the player when hit. `NPC_NAI` stood still 18 s and followed a driven circle to within 0.1 m. |
| Ghost copies the local player's look (`actor:MakeLookAsActor`), all ghosts male | The player's request. |
| Ghost velocity measured over at least 0.12 s; motion on real time with dead reckoning for one packet interval; 0.1 s follow | Live: rendered speed read 0 between packets while walking a circle, so the walk animation never played. Simulated at 60 fps, 1.5 m/s: stock draws walking pace on 16% of frames and stands on 67 of 330; this draws it on 100% and never stands. |
| Swing cue plays a real attack clip on animation layer 10 | Live: on layer 0 the actor's state restarts the clip; the guard pose on layer 4 hides anything below (on layer 1 only the sound came through); on layer 10 the player saw the swing. |
| Host world: the host is a dedicated machine nobody plays on (`mp_world_role host`, set only by its launcher) and tracks 40 NPCs / 60 m; a game that sees a `[HOST]`-named peer is a guest and emits nothing; a player's machine is never a host by itself — with no host in the session it follows stock rules | Live, stock rules: 99 puppet grabs and 92 releases in 631 s (8.7 a minute), 54 of 82 re-grabbed within 10 s, 8 NPCs driven from both sides. |
| NPC puppets: 8 s hold (was 3), continuous motion, 33 ms tick, left alone while in a conversation | A standing NPC is reported every 2 s, so a 3 s hold dropped it on one late packet. Simulated at 4 Hz packets: stock draws walking pace on 13% of frames; this on 100%. |
| Host follow (`mp_world_host_follow`) | A dedicated host's game only simulates NPCs around its own player. **Untested on a live host.** |

Tests: `python tools/Test-GamePassLua.py` runs the whole script under Lua 5.1
with the game stubbed; against stock 0.18.2 it fails, against this it passes.

## Scripts

- `package/Setup.ps1` — one installer: finds the game on the machine and offers Game Pass player / Steam player / world host. Detection is live-tested on a Game Pass machine; the Steam branch has never run.
- `package/Start-GamePass.ps1` — Game Pass guest launcher (live-tested).
- `package/Start-WorldHost.ps1` — dedicated world host on a Steam machine (**never run**).
- `tools/Build-HostWorldRelease.ps1` — builds `release/KCDMP-<VERSION>-HostWorld.zip`.

## Not changed

`dotnet/KcdMp.Protocol`, `dotnet/KcdMp.Server`, the launcher, the installer
and the native plugin.
