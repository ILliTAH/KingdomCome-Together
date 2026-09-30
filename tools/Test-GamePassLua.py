"""Game Pass fork: offline checks for the kdcmp.lua ghost changes.

Runs the whole mod under Lua 5.1 (lupa) with every game API stubbed, drives
os.clock by hand, and checks the three things the fork changed:
  * every ghost is male,
  * velocity does not spike when packets land one frame apart,
  * walk/run does not flip on every noisy sample.
Usage:  python tools/Test-GamePassLua.py [path-to-kdcmp.lua]
Needs:  pip install lupa
"""
import sys
from lupa import lua51

SRC = sys.argv[1] if len(sys.argv) > 1 else r"kdcmp\Data\Scripts\Startup\kdcmp.lua"

lua = lua51.LuaRuntime(unpack_returned_tuples=True)
lua.execute(r"""
    TEST_NOW = 0
    os.clock = function() return TEST_NOW end
    local function stub()
        local t
        t = setmetatable({}, {
            __index = function() return t end,
            __call = function() return t end,
            __concat = function(a, b) return tostring(a == t and "" or a) .. tostring(b == t and "" or b) end,
            __tostring = function() return "stub" end,
        })
        return t
    end
    STUB = stub()
    -- 0.18.2's every-40th-packet debug line reads undeclared globals; in the
    -- game that errors inside the caller's pcall at the function's last line.
    raw_vx, raw_vy = 0, 0
    setmetatable(_G, { __index = function(_, k) return STUB end })
""")
ok, err = lua.eval("function(s) local f, e = loadstring(s, 'kdcmp.lua'); if not f then return false, e end; local ok, e2 = pcall(f); return ok, tostring(e2) end")(
    open(SRC, "rb").read())
assert ok, f"loading kdcmp.lua failed: {err}"

failures = []

def check(name, cond, detail=""):
    print(("PASS " if cond else "FAIL ") + name + (f"  ({detail})" if detail else ""))
    if not cond:
        failures.append(name)

# --- 1. always male -----------------------------------------------------------
male = set(str(g) for g in lua.eval("function() local s = {} for _, p in ipairs(KCD2MP.faceRoster.male) do s[#s+1] = p[2] end return s end")().values())
picks = [lua.eval("KCD2MP_PickFaceForPlayer")(n) for n in [f"player{i}" for i in range(300)] + ["Player One", "Henry", ""]]
check("every pick is class NPC", all(p["className"] == "NPC" for p in picks))
check("every pick is a male roster soul", all(str(p["guid"]) in male for p in picks))
check("pick is deterministic per name", lua.eval("KCD2MP_PickFaceForPlayer")("Player One")["guid"] == lua.eval("KCD2MP_PickFaceForPlayer")("Player One")["guid"])

# --- ghost harness ------------------------------------------------------------
lua.execute(r"""
    -- Same istate shape KCD2MP_SpawnGhost builds.
    function TEST_NewGhost(id, x, y, z)
        x, y, z = x or 100, y or 200, z or 50
        KCD2MP.ghosts[id] = { entity = STUB, spawnName = "kcd2mp_" .. id, istate = {
            px = x, py = y, pz = z, pr = 0, tx = x, ty = y, tz = z, tr = 0,
            cx = x, cy = y, cz = z, cr = 0, alpha = 1.0, alphaStep = 0.25,
            vx = 0, vy = 0, vz = 0, lastPacketX = x, lastPacketY = y,
            ticksSincePacket = 0, packetCount = 0, animTag = "idle", smoothedSpeed = 0,
            prevCx = x, prevCy = y, speedDropTicks = 0, spawnedAtClock = -100 } }
        return KCD2MP.ghosts[id]
    end
""")

def run_stream(arrivals, speed_mps=1.5, period=0.125):
    """Ghost walks along x at speed; packet k is sent at k*period and lands at arrivals[k]."""
    lua.execute("TEST_NewGhost('9')")
    ghost = lua.eval("KCD2MP.ghosts['9']")
    speeds = []
    for k, t in enumerate(arrivals):
        lua.globals().TEST_NOW = t
        x = 100.0 + speed_mps * k * period
        lua.eval("KCD2MP_UpdateGhost")("9", x, 200.0, 50.0, 0.0, False)
        ist = ghost["istate"]
        vx = ist["vx"] or 0
        speeds.append(abs(vx))
    return speeds

# Packets every 125 ms, but delivered in RemoteConsole frames: alternately one
# frame (33 ms) and three frames (217 ms) after the previous one.
even = [0.125 * k for k in range(80)]
bursty = []
t = 0.0
for k in range(80):
    bursty.append(t)
    t += 0.033 if k % 2 == 0 else 0.217
v_even = run_stream(even)[10:]
v_burst = run_stream(bursty)[10:]
check("steady packets read ~1.5 m/s", all(1.2 <= v <= 1.8 for v in v_even), f"min={min(v_even):.2f} max={max(v_even):.2f}")
check("bursty packets never read above 2.2 m/s", max(v_burst) <= 2.2, f"max={max(v_burst):.2f}")
check("bursty packets never read below 0.9 m/s", min(v_burst) >= 0.9, f"min={min(v_burst):.2f}")

# --- 3. dwell -----------------------------------------------------------------
lua.execute(r"""
    function TEST_Flips(samples, dt)
        local g = TEST_NewGhost('8')
        g.istate.animTag = 'walk'
        local flips, last = 0, 'walk'
        for i, spd in ipairs(samples) do
            TEST_NOW = i * dt
            g.istate.smoothedSpeed = spd
            KCD2MP_UpdateAnimation('8', g)
            if g.istate.animTag ~= last then flips = flips + 1; last = g.istate.animTag end
        end
        return flips, last
    end
""")
noisy = lua.table_from([1.3 if i % 3 else 2.9 for i in range(100)])  # a lone run-speed spike every 0.3 s
flips, _ = lua.eval("TEST_Flips")(noisy, 0.1)
check("single-sample spikes do not flip walk/run", flips == 0, f"flips={flips}")
sustained = lua.table_from([1.3] * 10 + [2.9] * 20)
flips, last = lua.eval("TEST_Flips")(sustained, 0.1)
check("a sustained change still switches", flips == 1 and last == "run", f"flips={flips} last={last}")

# --- 4. rendered motion between packets -------------------------------------------
# The whole interp tick, 60 frames a second, against a peer who walks 8 s at
# 1.5 m/s and then stops. Packets leave every 105 ms and land with up to 30 ms
# of jitter -- the shape measured live. What matters is what gets drawn: is the
# stand-in moving at walking pace on every frame, or stepping and standing?
lua.execute(r"""
    Physics = { RayWorldIntersection = function() return nil end }
    Terrain = { GetElevation = function() return 50 end }
    KCD2MP.ghosts = {}   -- drop the earlier sections' ghosts
    local NOP
    NOP = setmetatable({}, { __index = function() return NOP end, __call = function() return nil end })
    function TEST_NewPuppet(id, x, y, z)
        local g = TEST_NewGhost(id, x, y, z)
        g.entity = setmetatable({
            id = 7,
            actor = setmetatable({ IsDead = function() return false end, IsUnconscious = function() return false end },
                                 { __index = function() return NOP end }),
            SetWorldPos = function(self, p) TEST_DRAWN = { x = p.x, y = p.y, z = p.z } end,
            GetWorldPos = function() return TEST_DRAWN or { x = x, y = y, z = z } end,
            GetAnimationLength = function(self, slot, name) return 1.2 end,
            StartAnimation = function(self, slot, name, layer) TEST_ANIMS[#TEST_ANIMS + 1] = { name = name, layer = layer } end,
        }, { __index = function() return NOP end })
        KCD2MP.interpRunning = true
        TEST_ANIMS = {}
        return g
    end
""")

def simulate(walk_s=8.0, total_s=12.0, speed=1.5, period=0.105, fps=60):
    lua.globals().TEST_DRAWN = None
    ghost = lua.eval("TEST_NewPuppet")("7", 100.0, 200.0, 50.0)
    seed = 12345
    packets = []                      # (arrival time, x)
    k = 0
    while k * period <= walk_s:
        seed = (seed * 1103515245 + 12345) % (2 ** 31)
        packets.append((k * period + 0.03 * seed / 2 ** 31, 100.0 + speed * k * period))
        k += 1
    final_x = packets[-1][1]
    frames, prev_x, t, pi = [], 100.0, 0.0, 0
    while t < total_s:
        lua.globals().TEST_NOW = t
        while pi < len(packets) and packets[pi][0] <= t:
            lua.eval("KCD2MP_UpdateGhost")("7", packets[pi][1], 200.0, 50.0, 0.0, False)
            pi += 1
        lua.eval("KCD2MP_InterpTick")("ext")
        x = ghost["istate"]["cx"]
        frames.append((t, x, (x - prev_x) * fps, ghost["istate"]["animTag"]))
        prev_x = x
        t += 1.0 / fps
    return frames, final_x

frames, final_x = simulate()
steady = [f for f in frames if 2.0 <= f[0] <= 7.5]
paced = sum(1.0 <= f[2] <= 2.0 for f in steady) / len(steady)
standing = sum(f[2] < 0.3 for f in steady)
walking = sum(f[3] == "walk" for f in steady) / len(steady)
after = [f for f in frames if f[0] > 8.0]
overshoot = max(f[1] for f in frames) - final_x
check("moving at walking pace on at least 95% of frames", paced >= 0.95, f"{paced:.0%} of frames between 1.0 and 2.0 m/s; slowest {min(f[2] for f in steady):.2f}, fastest {max(f[2] for f in steady):.2f}")
check("never standing still mid-walk", standing == 0, f"{standing} of {len(steady)} frames under 0.3 m/s")
check("walk animation holds while walking", walking >= 0.95, f"{walking:.0%} of frames tagged walk")
check("overshoot at a stop stays under 0.3 m", overshoot <= 0.30, f"{overshoot:.2f} m")
check("settles on the last reported position", abs(after[-1][1] - final_x) <= 0.05, f"off by {abs(after[-1][1] - final_x):.3f} m")
check("idle once stopped", after[-1][3] == "idle", f"tag={after[-1][3]}")

# --- 5. a peer's swing ---------------------------------------------------------
# Seen live: the swing only shows on a layer above the guard pose (layer 4) --
# layer 10 is the one the player saw -- and must not freeze the ghost in place.
ghost = lua.eval("TEST_NewPuppet")("6", 100.0, 200.0, 50.0)
lua.eval("KCD2MP_GhostCombat")("6", 2)
anims = [(a["name"], a["layer"]) for a in lua.eval("TEST_ANIMS").values()]
check("a swing plays a real attack clip on the overlay layer",
      anims == [("combat_rg_sz1_az2_natksw_medium_lngsw", 10)], f"{anims}")
check("a swing does not freeze the ghost", ghost["istate"]["oneShotUntil"] is None,
      f"oneShotUntil={ghost['istate']['oneShotUntil']}")

# --- 6. host world ----------------------------------------------------------------
# One game decides where the NPCs are. A Modding Tools build is the host and
# reports its neighbourhood; a retail / Game Pass build is the guest and only
# displays. (docs/superpowers/specs/2026-09-30-host-world-npc-sync-design.md)
lua.execute(r"""
    local NOP
    NOP = setmetatable({}, { __index = function() return NOP end, __call = function() return nil end })
    TEST_EVENTS, TEST_NPCS, TEST_NPC_POS = {}, {}, {}
    KCD2MP_EmitEvent = function(name, arg) TEST_EVENTS[#TEST_EVENTS + 1] = name .. " " .. tostring(arg) end
    player = setmetatable({ id = 1, GetWorldPos = function() return { x = 0, y = 0, z = 0 } end,
                            actor = { GetHealth = function() return 100 end } },
                          { __index = function() return NOP end })
    function TEST_MakeNpc(name, x, y)
        TEST_NPC_POS[name] = { x = x, y = y, z = 0 }
        local e = setmetatable({
            class = "NPC", id = name,
            GetName = function() return name end,
            GetWorldPos = function() local p = TEST_NPC_POS[name]; return { x = p.x, y = p.y, z = p.z } end,
            GetWorldAngles = function() return { x = 0, y = 0, z = 0 } end,
            SetWorldPos = function(self, p) TEST_NPC_POS[name] = { x = p.x, y = p.y, z = p.z } end,
            GetAnimationLength = function() return 1.0 end,
            actor = { GetHealth = function() return 100 end, IsDead = function() return false end,
                      IsUnconscious = function() return false end },
            human = { IsWeaponDrawn = function() return false end },
        }, { __index = function() return NOP end })
        TEST_NPCS[name] = e
        return e
    end
    STUB.GetEntityByName = function(name) return TEST_NPCS[name] end
    STUB.GetEntitiesInSphere = function(pos, radius)
        local out = {}
        for name, e in pairs(TEST_NPCS) do
            local p = TEST_NPC_POS[name]
            if math.sqrt(p.x * p.x + p.y * p.y) <= radius then out[#out + 1] = e end
        end
        return out
    end
    function TEST_ResetWorld(role_xgen)
        TEST_EVENTS, TEST_NPCS, TEST_NPC_POS = {}, {}, {}
        KCD2MP.npcTracked, KCD2MP.npcPuppets, KCD2MP.ghosts = {}, {}, {}
        KCD2MP._npcScanAt = -100
        KCD2MP.npcSyncRunning, KCD2MP.npcSync.enabled = true, true
        XGenAIModule = role_xgen and { SpawnEntity = function() end } or {}
        KCD2MP.worldRole = "auto"
    end
    function TEST_Count(t) local n = 0 for _ in pairs(t) do n = n + 1 end return n end
""")
role = lua.eval("KCD2MP_WorldRole")
lua.eval("TEST_ResetWorld")(True)
check("a Modding Tools build is the host", role() == "host", role())
lua.eval("TEST_ResetWorld")(False)
check("a retail / Game Pass build is the guest", role() == "guest", role())
lua.execute('KCD2MP.worldRole = "host"')
check("the role can be overridden", role() == "host", role())

for authority in (True, False):
    lua.eval("TEST_ResetWorld")(False)
    lua.globals().KCD2MP.hitSensorOn = authority
    lua.execute("TEST_NewGhost('5')")           # a peer is present, so stock 0.18.2 would claim
    for i in range(60):
        lua.eval("TEST_MakeNpc")(f"ttkc_man_{i}", 1.0 + i * 0.4, 0.0)
    for k in range(5):
        lua.globals().TEST_NOW = 100.0 + k * 2.5
        lua.eval("KCD2MP_NpcSyncTick")()
    events = list(lua.eval("TEST_EVENTS").values())
    check(f"a guest reports no NPCs (relay authority={authority})", len(events) == 0,
          f"{len(events)} events, first: {events[:1]}")

lua.eval("TEST_ResetWorld")(True)
lua.globals().KCD2MP.hitSensorOn = True
for i in range(60):
    lua.eval("TEST_MakeNpc")(f"ttkc_man_{i}", 1.5 * (i + 1), 0.0)      # 1.5 m .. 90 m away
lua.globals().TEST_NOW = 200.0
lua.eval("KCD2MP_NpcSyncTick")()
tracked = list(lua.eval("KCD2MP.npcTracked").keys())
far = [n for n in tracked if lua.eval("TEST_NPC_POS")[n]["x"] > 60.0]
check("a host tracks 40 NPCs", len(tracked) == 40, f"{len(tracked)} tracked")
check("a host tracks nothing beyond 60 m", not far, f"{far[:3]}")
check("a host reports each tracked NPC", len(list(lua.eval("TEST_EVENTS").values())) == 40,
      f"{len(list(lua.eval('TEST_EVENTS').values()))} events")

def simulate_puppet(walk_s=8.0, total_s=11.0, speed=1.4, period=0.25, fps=30):
    lua.eval("TEST_ResetWorld")(False)
    lua.eval("TEST_MakeNpc")("ttkc_man_1", 10.0, 5.0)
    seed, packets, k = 777, [], 0
    while k * period <= walk_s:
        seed = (seed * 1103515245 + 12345) % (2 ** 31)
        packets.append((k * period + 0.03 * seed / 2 ** 31, 10.0 + speed * k * period))
        k += 1
    frames, prev, t, pi = [], 10.0, 0.0, 0
    while t < total_s:
        lua.globals().TEST_NOW = 300.0 + t
        while pi < len(packets) and packets[pi][0] <= t:
            lua.eval("KCD2MP_ApplyNpcState")("ttkc_man_1", packets[pi][1], 5.0, 0.0, 0.0, 100.0, 0)
            pi += 1
        lua.eval("KCD2MP_NpcPuppetTick")("ext")
        x = lua.eval("TEST_NPC_POS")["ttkc_man_1"]["x"]
        p = lua.eval("KCD2MP.npcPuppets")["ttkc_man_1"]
        frames.append((t, x, (x - prev) * fps, p["animTag"] if p else None))
        prev = x
        t += 1.0 / fps
    return frames, packets[-1][1]

frames, final_x = simulate_puppet()
steady = [f for f in frames if 2.0 <= f[0] <= 7.5]
paced = sum(0.9 <= f[2] <= 1.9 for f in steady) / len(steady)
check("an NPC puppet is drawn at walking pace on at least 90% of frames", paced >= 0.90,
      f"{paced:.0%}; slowest {min(f[2] for f in steady):.2f}, fastest {max(f[2] for f in steady):.2f}")
check("an NPC puppet never stands still mid-walk", sum(f[2] < 0.3 for f in steady) == 0,
      f"{sum(f[2] < 0.3 for f in steady)} of {len(steady)} frames under 0.3 m/s")
check("an NPC puppet walks while walking", sum(f[3] == "walk" for f in steady) / len(steady) >= 0.9,
      f"{sum(f[3] == 'walk' for f in steady) / len(steady):.0%} of frames tagged walk")
check("an NPC puppet settles where the host left it", abs(frames[-1][1] - final_x) <= 0.1,
      f"off by {abs(frames[-1][1] - final_x):.2f} m")

lua.eval("TEST_ResetWorld")(False)
lua.eval("TEST_MakeNpc")("ttkc_man_2", 3.0, 3.0)
lua.globals().TEST_NOW = 500.0
lua.eval("KCD2MP_ApplyNpcState")("ttkc_man_2", 3.0, 3.0, 0.0, 0.0, 100.0, 0)
lua.globals().TEST_NOW = 507.0
lua.eval("KCD2MP_NpcPuppetTick")("ext")
held = lua.eval("KCD2MP.npcPuppets")["ttkc_man_2"] is not None
lua.globals().TEST_NOW = 509.0
lua.eval("KCD2MP_NpcPuppetTick")("ext")
released = lua.eval("KCD2MP.npcPuppets")["ttkc_man_2"] is None
check("a puppet is held through 7 s of silence", held)
check("a puppet is released after 9 s of silence", released)

# A dedicated world host: a peer named "[HOST]..." makes everyone else a guest,
# a Modding Tools player included, and gets no stand-in.
lua.eval("TEST_ResetWorld")(True)
lua.execute("KCD2MP.worldHostIds = {} TEST_NewGhost('3')")
lua.eval("KCD2MP_SetGhostName")("3", "[HOST] world")
check("a [HOST] peer makes a Modding Tools player a guest", role() == "guest", role())
check("a [HOST] peer's stand-in is removed", lua.eval("KCD2MP.ghosts")["3"] is None)
lua.eval("KCD2MP_UpdateGhost")("3", 1.0, 2.0, 3.0, 0.0, False)
check("a [HOST] peer is never given a stand-in", lua.eval("KCD2MP.ghosts")["3"] is None)
lua.eval("KCD2MP_SetGhostName")("4", "Friend")
check("an ordinary peer's name changes nothing", role() == "guest" and lua.eval("KCD2MP.worldHostIds")["4"] is None)
lua.eval("KCD2MP_RemoveGhost")("3")
check("when the [HOST] peer leaves, the build decides again", role() == "host", role())

# Where a dedicated host stands: among the guests when they are together,
# with the lowest-numbered guest when they are apart.
target = lua.eval("function() local x, y, z = KCD2MP_WorldHostFollowTarget() return x, y, z end")
lua.execute("KCD2MP.ghosts = {}")
check("no guests, no follow target", target() in (None, (None, None, None)), f"{target()}")
lua.execute("TEST_NewGhost('7', 0, 0, 5) TEST_NewGhost('9', 10, 0, 5)")
tx, ty, tz = target()
check("together: the host stands in the middle", (tx, ty, tz) == (5.0, 0.0, 5.0), f"{(tx, ty, tz)}")
lua.execute("KCD2MP.ghosts = {} TEST_NewGhost('7', 0, 0, 5) TEST_NewGhost('9', 100, 0, 5)")
tx, ty, tz = target()
check("apart: the host stays with the lowest-numbered guest", (tx, ty, tz) == (0.0, 0.0, 5.0), f"{(tx, ty, tz)}")

# An NPC the local player is talking to is left where it is.
lua.eval("TEST_ResetWorld")(False)
npc = lua.eval("TEST_MakeNpc")("ttkc_man_3", 3.0, 3.0)
lua.execute("TEST_NPCS['ttkc_man_3'].human = { IsInDialog = function() return true end, IsWeaponDrawn = function() return false end }")
lua.globals().TEST_NOW = 600.0
lua.eval("KCD2MP_ApplyNpcState")("ttkc_man_3", 3.0, 3.0, 0.0, 0.0, 100.0, 0)
lua.globals().TEST_NOW = 600.3
lua.eval("KCD2MP_ApplyNpcState")("ttkc_man_3", 4.5, 3.0, 0.0, 0.0, 100.0, 0)
for k in range(10):
    lua.globals().TEST_NOW = 600.3 + 0.033 * (k + 1)
    lua.eval("KCD2MP_NpcPuppetTick")("ext")
pos = lua.eval("TEST_NPC_POS")["ttkc_man_3"]
check("an NPC in a conversation is not moved", (pos["x"], pos["y"]) == (3.0, 3.0), f"{(pos['x'], pos['y'])}")

print("\n" + ("ALL PASS" if not failures else f"{len(failures)} FAILED: {failures}"))
sys.exit(1 if failures else 0)
