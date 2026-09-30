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
| The DLL pipe is never looked for in RemoteConsole mode (`CombatPipe.Disabled`); elsewhere a failed look is not repeated for 10 s; NPC swings keep the Lua cue on Game Pass | Review finding: damage, death and hit packets each waited 500 ms for a pipe that was not there, inside the loop that applies every other packet. |
| The position heartbeat goes by the clock (2 s), not by loop ticks | Review finding: guests count the host as present by these packets, and a tick is 10 ms only on paper -- about 15.6 ms on Windows, and a host's tick also waits for an HTTP round trip. |
| A player's name never starts with `[HOST]` (`ClientConfig.OrdinaryPlayerName`) | Review finding: every other game would take that player for the host and hide them. |
| `--world-host` / `--no-world-follow`: the agent asserts the host role and the follow loop on its re-arm cadence, and connects under a `[HOST]` name | A save load kills the mod's timers and a game restart forgets the role; nobody is at a dedicated host to set them again. Command line only, never the config file. |
| A disconnected peer's name is forgotten | The name re-assert kept telling the mod that a departed `[HOST]` peer was still there. |

Tests: `dotnet test dotnet/KcdMp.Client.Tests` (43), including a fake
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
| Host follow (`mp_world_host_follow`) | A dedicated host's game only simulates NPCs around its own player. **Untested on a live host.** The loop carries a heartbeat and a generation, so a save load cannot leave it dead and off-then-on cannot leave two (live on Game Pass through the console: it keeps ticking and a second assertion starts nothing). |
| A `[HOST]` peer counts only once it has reported a position and while its packets keep arriving (30 s); a name alone, or the name of an id that left, makes no guest; an ordinary name on that id clears it (the rules were called in the live Game Pass game through the console and answered as the tests do) | Review finding: the stock agent keeps a departed peer's name and re-sends it every 2.5 s, so every player stayed a guest of a host that was gone, and a new player given that id was hidden. |

Tests: `python tools/Test-GamePassLua.py` runs the whole script under Lua 5.1
with the game stubbed; against stock 0.18.2 it fails, against this it passes.

## Launcher (`KCDMP_launcher`) and installer (`installer/KCDMP.iss`)

The goal: one Setup exe, nothing else to install first, the stock launcher's menu.

| Change | Why (evidence) |
|---|---|
| `GameInstalls`: finds the Steam Modding Tools build (registry, `libraryfolders.vdf`, a shallow scan) and the Xbox Game Pass build (each drive's `.GamingRoot`); `Platform` setting `auto`/`steam`/`gamepass`; Settings shows both paths | The stock launcher knew only the Steam build, and only through a path its installer seeded. |
| `GameInstalls.DiagnoseSteam` / `SteamAdvice`: Settings says which of three things is missing (no Steam; Modding Tools never installed; listed by Steam but not on disk) with GET THE MODDING TOOLS ON STEAM and LOOK AGAIN; Steam is looked up per user and then machine-wide; the tools are found through their appmanifest (`2429020`) | First run on a server machine: the launcher opened on Settings with two empty path boxes and nothing to go on. The stock installer had this diagnosis in its gate page, which this fork's installer no longer has. |
| Report Bug and View Releases point at this fork | The stock ones send fork users, and fork bugs, to the original project. |
| `ModPackage`: the mod (the manifest and the pak, nothing loose) and the skip save go into the game when it is launched | At install time, as stock did, an update of the launcher can leave the game on an older mod; and the installer would have to find the game. |
| JOIN on Game Pass runs `Start-GamePass.ps1` (mod, `-devmode`, auto-load, agent over RemoteConsole); no CONNECT step | No plugin to inject there; the agent waits for the save by itself. Live: installer, launcher, JOIN SERVER, the game loaded the save by itself, the agent printed `Connected!` against a local relay. |
| HOST GAME has RUN AS WORLD HOST, which runs `Start-WorldHost.ps1` | One implementation of the host, reachable from the menu. **Never run** (no Steam build on the authoring machine). |
| `KCDMP_launcher.exe --play host[:port]` | Joins without the window (Game Pass), for a shortcut. |
| The launcher sets its working directory to its own folder | settings.json and the server lists are bare relative names. |
| Installer: no Modding Tools gate, own AppId and a fixed folder (`%LOCALAPPDATA%\KCDMP-HostWorld`; no folder page, because the uninstaller removes the whole folder), ships the scripts, the mod and the save; the uninstaller removes the auto-load line from the game and asks before removing the mod | The stock installer refuses to continue without Steam's Modding Tools. Live on the Game Pass machine: silent install (size check passed, 1037 files), upgrade in place, silent uninstall (files, registry key and the `user.cfg` line gone, mod left). |
| The native plugin and injector are built from the unchanged 0.18.2 sources with Visual Studio 2019 (MSVC 19.29) | No binaries are taken from upstream's installer. **Never run in a game**: the Steam path cannot be tested on the authoring machine. |

Removed: `installer/SteamDetect.iss` and its tests (the gate they tested is gone), `tools/Test-Installer*.ps1`.

Tests: `dotnet test dotnet/KcdMp.Launcher.Tests` (36).

## Scripts (installed beside the launcher)

- `package/Start-GamePass.ps1` — what JOIN runs for a Game Pass player (live-tested, also from the launcher). The game loads the newest save by itself: `wh_sys_AutoLoadLastSave = 1` in `user.cfg` next to the exe. Live: the load began 2 log lines after the main menu, against 19–35 lines in five runs where the player pressed Continue. The same cvar as `+wh_sys_AutoLoadLastSave 1` on the command line is applied after the menu exists and did nothing (the game sat at the menu for about 100 s). Only those two lines of `user.cfg` are ever written; every other byte is kept, in whatever encoding the file has.
- `package/Start-WorldHost.ps1` — dedicated world host on a Steam machine (**never run**). Installs the skip save before the game starts (the save list is read once), loads it with `wh_sys_LoadGame` over `:1403` (sent again only if the game's log shows it never took the first), minimises the window, starts the game from the folder that holds `steam_appid.txt`, injects the native plugin, and runs the agent with `--world-host`. KCD2 has no dedicated-server program and needs a GPU; upstream measured a software renderer at 15–55 times slower than real time.
- `package/Uninstall-GameChanges.ps1` — run by the uninstaller.
- Steam and the host (`Start-WorldHost.ps1`, `Get-SteamRoot`, `Test-LogContains`): the first real run on a server stopped at the game's "License not verified / No SteamApps" box. Upstream's notes record that box for a game started while the Steam client was not running (`SteamApi_Init failed` in `kcd.log`). The script now starts Steam when none runs in this Windows session, closes and restarts a game whose log holds that line (three attempts, then a message that says what to do), and closes a game it started when the debug API never comes up. The launcher refuses a Steam JOIN while Steam is not running. **Never run**: written from that one screenshot and upstream's note; the server's own log has not been seen.
- `package/save/autosave018.whs` — the skip save upstream publishes with 0.18.2, installed as `kcdmpskip.whs` so that it never replaces a save of the same number.
- `tools/Import-GamePassSave.py` — puts a `.whs` into the Game Pass save containers (used on one live machine; backs the containers up first; refuses while the game runs).
- `tools/Test-PackageScripts.ps1` — offline checks for the package scripts, including that every command they call exists.
- `tools/Build-Installer.ps1` — builds `release/KingdomCome-Coop-Setup-<VERSION>.exe`.

## Not changed

`dotnet/KcdMp.Protocol`, `dotnet/KcdMp.Server`, `dotnet/KcdMp.MasterServer` and the
native plugin's sources.
