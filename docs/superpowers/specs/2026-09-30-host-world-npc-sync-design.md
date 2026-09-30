# Host world: one game decides where the NPCs are — design

Date: 2026-09-30 · Branch: `gamepass-rc` · Status: approach and host chosen by the user in chat

## Goal

When the two players are together, the NPCs around them are in the same place
on both screens and move continuously, instead of warping. The user's words:
"make it a central world — still and smooth".

A real central server is not possible: KCD2 has no server build, and a world is
only simulated by a running game with a player standing in it. So one player's
game is the world ("host") and the other displays it ("guest"). The user chose
**the Steam player's game as host**; the Game Pass game is the guest.

## What the live session showed (631 s, this branch, stock mod on the Steam side)

| Measured | Value |
|---|---|
| NPC puppets started / released | 99 / 92 on 27 NPCs — 8.7 releases a minute |
| Released and re-grabbed within 10 s | 54 of 82 (33 within 2 s) |
| NPCs both emitted here and driven here | 8 |
| Tracked set per player | 5 NPCs within 30 m |

Each release snaps the NPC back to where this game's own AI has it, and each
grab snaps it to the stream: those two jumps are the warps. Three causes, all in
the mod's Lua: a standing NPC is reported every 2 s but released after 3 s of
silence; only 5 NPCs are tracked, so the set churns as players move; and both
players emit for the NPCs near them, so ownership flips.

## Design

All changes are in `kdcmp.lua`; one pak serves both sides. The relay and the
Steam agent stay stock — the relay already routes NPC state per entity with no
count limit, and a guest that never sends cannot create a claim.

### Role

`KCD2MP_WorldRole()` returns `host` or `guest`. `KCD2MP.worldRole = "auto"`
derives it from the build: a game that has `XGenAIModule.SpawnEntity` (Modding
Tools) is a host; one that does not (retail, Game Pass) is a guest.
`mp_world_role host|guest|auto` overrides it for a session.

Two Modding Tools players are both hosts and behave as stock 0.18.2 does, with
the larger tracked set: the relay's per-entity claims still arbitrate.

### Host: emit the neighbourhood

The tracked set grows from 5 NPCs within 30 m to **40 within 60 m**
(`hostMaxTracked`, `hostRadius`); cadence stays 4 Hz on movement, 2 s heartbeat.
Nothing else on the emitting side changes.

### Guest: display only

- **Never emits** NPC state, proximity claims or drag claims, so nothing it
  does can take an NPC away from the host.
- **Holds a puppet 8 s** without a packet (was 3 s) — three missed heartbeats.
- **Moves puppets continuously**, the same model as the ghosts: velocity from
  packets at least 0.12 s apart, the target carried forward for one packet
  interval, eased back when the next packet is late, followed with a 0.15 s
  time constant on real time; the puppet tick runs every 33 ms (was 50).
  A target more than 5 m away still snaps.
- Animation comes from the smoothed rendered speed, not from one tick's step.

## Known limits (stated to the user before work started)

- The players must stay in the same area: the host's game only simulates NPCs
  around the host. Beyond ~60 m from the host the guest sees its own world.
- An NPC's activity (sitting, working, talking) is not reproduced: a puppet
  shows walk / run / idle / weapon-ready.
- Quests, dialogue, shops and containers stay per player.
- NPC damage and death across machines remain unavailable on the Game Pass
  side (native plugin); untested Lua routes exist and are out of scope here.
- The guest's own AI still tries to move a puppeted NPC between writes.

## Testing

- `tools/Test-GamePassLua.py`: role auto-detection both ways and the override;
  a guest emits nothing with NPCs all around it; a host tracks 40 of 60
  candidates and never one beyond the radius; a puppet fed 4 Hz packets at
  1.4 m/s is drawn at walking pace on at least 90% of frames and never stands
  mid-walk; a puppet survives a 7 s gap and is released after 9 s.
- Live: the Steam player installs the pak; the release rate is measured again
  against the 8.7 a minute above, and the user judges the warping.
