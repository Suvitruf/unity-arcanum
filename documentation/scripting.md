# Scripting — the `.scr` VM and the heartbeat clock

In *Arcanum*, almost everything an object "does" beyond moving and fighting is driven by a tiny
attached script. A door that teleports you to an interior, a lever that opens a gate, an NPC that
greets you, a sign you can read, a quest that advances, a body that is already dead when you walk
in — all of these are **compiled `.scr` programs** fired at well-defined moments. This page
describes how those scripts are stored, how the engine runs them, and the clock (`ai_timeevent`)
that ticks objects so their heartbeat scripts can run.

The short version:

- A `.scr` file is a flat **list of condition → action entries**, each with an *else* action.
- Scripts hang off an object at named **attachment points** (`SAP_USE`, `SAP_EXAMINE`,
  `SAP_HEARTBEAT`, `SAP_DIALOG`, …). The event that fired decides which script runs.
- Execution walks the list top-to-bottom; each entry's action returns a **control code** that says
  advance, jump, or stop — and stopping can optionally tell the engine to still run the object's
  built-in default behaviour.
- A focus-object **loop family** (`SAT_LOOP_FOR` / `_END` / `_BREAK`) lets one entry iterate over a
  *set* of nearby objects (party members, critters in vicinity, …).
- Living objects get a **heartbeat** on a distance-scaled timer: 0.25 s when adjacent to the player,
  up to 5 s far away. Heartbeat scripts are how NPCs self-manage — including the **self-gating**
  pattern where an NPC placed in the map removes or kills itself unless a quest flag says it should
  stay.

Source references point at `arcanum-ce` (`src/game/script.c`, `script.h`, `ai.c`).

## Contents

- [Attachment points (`SAP_*`)](#attachment-points-sap_)
- [`.scr` file structure](#scr-file-structure)
- [Operands and where values live](#operands-and-where-values-live)
- [How a script executes](#how-a-script-executes)
- [Control flow and return codes](#control-flow-and-return-codes)
- [The focus-object loop family](#the-focus-object-loop-family)
- [The object heartbeat clock (`ai_timeevent`)](#the-object-heartbeat-clock-ai_timeevent)
- [Self-gating NPCs](#self-gating-npcs)

## Attachment points (`SAP_*`)

Every scriptable object carries a sparse table of script slots, one per **attachment point**. The
attachment point is the *event* — when that event happens to the object, the engine runs whatever
script sits in that slot (and slots left empty simply do nothing). The same object can have a
different script on `SAP_USE`, `SAP_EXAMINE`, and `SAP_HEARTBEAT` at once.

The full set (order is the engine's enum, `script.h:7`):

| Attachment point         | Fires when…                                                            |
| ------------------------ | ---------------------------------------------------------------------- |
| `SAP_EXAMINE`            | The object is looked at / right-clicked (fires at range; floats sign and scenery text). |
| `SAP_USE`               | The object is used — clicking a door, lever, container, switch.         |
| `SAP_DESTROY`           | The object is destroyed.                                                |
| `SAP_UNLOCK`            | A lock on the object is picked or opened.                               |
| `SAP_GET` / `SAP_DROP`  | An item is picked up / dropped.                                         |
| `SAP_THROW`             | The object is thrown.                                                   |
| `SAP_HIT` / `SAP_MISS`  | An attack involving the object lands / misses.                          |
| `SAP_DIALOG`            | A conversation with the object begins.                                  |
| `SAP_DIALOG_OVERRIDE`   | Replaces the normal dialog entry point (used to redirect conversations).|
| `SAP_FIRST_HEARTBEAT`   | The object's **first** heartbeat tick only (one-time setup).            |
| `SAP_HEARTBEAT`         | Every subsequent heartbeat tick (see the clock, below).                |
| `SAP_DYING`             | The critter is dying.                                                   |
| `SAP_RESURRECT`         | The critter is resurrected.                                             |
| `SAP_ENTER_COMBAT` / `SAP_EXIT_COMBAT` | This critter enters / leaves combat mode.               |
| `SAP_START_COMBAT` / `SAP_END_COMBAT`  | A combat encounter begins / ends.                       |
| `SAP_CRITTER_HITS`      | The critter scores a hit.                                               |
| `SAP_CRITICAL_HIT` / `SAP_CRITICAL_MISS` | A critical hit / fumble occurs.                        |
| `SAP_TAKING_DAMAGE`     | The critter is taking damage.                                           |
| `SAP_WILL_KOS`          | The critter is about to go kill-on-sight.                               |
| `SAP_WIELD_ON` / `SAP_WIELD_OFF` | An item is wielded / unwielded.                               |
| `SAP_INSERT_ITEM` / `SAP_REMOVE_ITEM` | An item is inserted into / removed from a container.    |
| `SAP_TRANSFER`          | Items are transferred (e.g. barter).                                    |
| `SAP_BUY_OBJECT`        | An object is bought from this critter.                                  |
| `SAP_NEW_SECTOR`        | The object (typically a follower) crosses into a new sector.            |
| `SAP_LEADER_KILLING` / `SAP_LEADER_SLEEPING` | The follower's leader is killing / sleeping.       |
| `SAP_CATCHING_THIEF_PC` / `SAP_CAUGHT_THIEF` | Theft is detected.                                  |
| `SAP_BUST`              | The critter is busted (caught committing a crime).                      |

The two most common in everyday play are `SAP_USE` (interacting) and the heartbeat pair, which the
clock below drives.

## `.scr` file structure

Compiled scripts live as `scr/<5-digit number><name>.scr` (e.g.
`scr/01267bates mansion to 1st floor_tel.scr`), referenced from an object by **number**. The slot
on the object records that number plus a small block of per-object script state (local flags and
counters). All values are little-endian.

On disk a `.scr` is an 8-byte **`ScriptHeader`** (the template's default per-object state), then a
**`ScriptFile`** (`script.h:319`), then the entries:

```
// ScriptHeader (8 bytes) — the script's default flags + counters. This is only a TEMPLATE; the live
// per-object copy is stored on the object itself (OBJ_F_SCRIPTS), seeded from here.
flags(4)
counters[4]       // four 8-bit counters, packed into one word

// ScriptFile:
description[40]   // human label, ignored at runtime
flags(4)
num_entries(4)    // how many condition entries follow
max_entries(4)
pad(4)            // unused on disk — the x86 ScriptFile::entries pointer slot
entries[num_entries]
```

Each entry is a `ScriptCondition` (132 bytes, `script.h:308`):

```
type(4)           // ScriptConditionType (SCT_*)
op_type[8]        // one byte per operand: how to interpret it
op_value[8]       // one int per operand: the operand value
action  (44)      // ScriptAction run when the condition is TRUE
els     (44)      // ScriptAction run when the condition is FALSE
```

And an action (`ScriptAction`, 44 bytes, `script.h:299`) has the same operand shape minus the two
sub-actions:

```
type(4)           // ScriptActionType (SAT_*)
op_type[8]
op_value[8]
```

So an entry is read as: *evaluate the condition; if true do `action`, otherwise do `els`*. Both the
condition and its chosen action carry up to eight operands.

## Operands and where values live

Each operand is a `(op_type, op_value)` pair. The `op_type` selects an addressing mode
(`ScriptValueType`, `script.h:73`), and the engine resolves the pair through `script_get_value` /
`script_set_value`:

| `op_type`     | Meaning                                                                            |
| ------------- | ---------------------------------------------------------------------------------- |
| `SVT_NUMBER`  | A literal: the value *is* `op_value`.                                               |
| `SVT_GL_VAR`  | Global variable `script_global_vars[op_value]` — a 2000-entry int array, saved with the game. |
| `SVT_LC_VAR`  | Local variable: per-*invocation* scratch (ten ints, zeroed at the start of each run, not saved). |
| `SVT_GL_FLAG` | Global flag: one bit, `script_global_flags[i/32] >> (i%32) & 1` (`script.c:416`).   |
| `SVT_COUNTER` | One of four 8-bit counters packed into the running script's per-object header (0–255 each). |
| `SVT_PC_VAR`  | A variable stored on a PC object (`OBJ_F_PC_GLOBAL_VARIABLES`); the operand also encodes which focus object. |
| `SVT_PC_FLAG` | A flag bit stored on a PC object (`OBJ_F_PC_GLOBAL_FLAGS`).                          |

Two important distinctions:

- **Global** vars/flags persist for the whole game and are what scripts use to remember world state
  ("the quest has begun", "the gate is open"). **Local** flags/counters live in the object's own
  script slot and persist on that object; **local vars** are throwaway scratch for the current run.
- **Object** operands are different. When an operand refers to an *object* (a critter, a portal, the
  player), `op_type` is not a `ScriptValueType` but a `ScriptFocusObject` (`SFO_*`, `script.h:46`):
  `SFO_TRIGGERER` (who set the script off), `SFO_ATTACHEE` (the object the script is on),
  `SFO_PLAYER`, `SFO_EXTRA_OBJECT`, `SFO_CURRENT_LOOPED_OBJECT`, and a family of *set* selectors
  (followers, party, team, and "in vicinity" sets of scenery / containers / portals / items).

The set selectors come in "any" and "every" forms — `SFO_ANYONE_IN_PARTY` vs
`SFO_EVERYONE_IN_PARTY`. For a *condition* over a set, "every" means all matched members must
satisfy it, "any" means one match is enough. For the loop family, the selector chooses which objects
the loop iterates over.

## How a script executes

When an event fires, the engine runs the matching script with four pieces of context: the
**triggerer** (who caused it), the **attachee** (the object the script lives on), an optional
**extra object**, and the **attachment point**. Execution (`script_execute`, `script.c`) is a loop:

1. Start at the entry index the caller asked for (`line`, usually 0).
2. Read that entry. Evaluate its condition (`script_execute_condition`, `script.c:579`).
3. Run `action` if the condition is true, otherwise `els`. The chosen action returns a **control
   code** (next section).
4. `NEXT` advances to the following entry; running off the end stops the script. A non-negative
   code jumps to that entry index. The two `RETURN_*` codes stop the script immediately.
5. A 1000-iteration guard stops a runaway script (`script.c:352`).

For conditions evaluated over a *set* of focus objects, the engine counts matches and treats the
condition as true when at least one matched and either every member matched or the selector was an
"any" selector.

## Control flow and return codes

Every action returns one of a handful of codes (`script.c:49`), and these are what give the flat
entry list its control flow:

| Code                          | Value | Effect                                                              |
| ----------------------------- | ----- | ------------------------------------------------------------------- |
| `NEXT`                        | −1    | Fall through to the next entry; off the end ⇒ stop (as skip-default). |
| *non-negative*                | ≥ 0   | **Goto**: jump to that entry index. This is what `SAT_GOTO` returns. |
| `RETURN_AND_SKIP_DEFAULT`     | −2    | Stop the script; the engine does **not** run the object's built-in default. |
| `RETURN_AND_RUN_DEFAULT`      | −3    | Stop the script; the engine **still** runs the object's default behaviour. |

The default-behaviour distinction matters. Consider a door with a `SAP_USE` script: if the script
decides the conditions for a special action (say, a teleport) are *not* met, it returns
"run default" so the engine still performs the ordinary open/close. A script that fully handled the
interaction itself returns "skip default" so the door does not also open. `SAT_GOTO` simply returns
the target entry index, which the loop interprets as a jump.

## The focus-object loop family

A single entry can iterate. Three action types form the loop (`script.c:1622`):

- **`SAT_LOOP_FOR`** resolves its operand into a *set* of focus objects (e.g. "every critter in
  vicinity", "everyone in party") and binds the first of them to `SFO_CURRENT_LOOPED_OBJECT`. The
  body of the loop is the entries that follow, which can read the current looped object. If the set
  is **empty**, the loop is skipped entirely — execution jumps past the matching `SAT_LOOP_END`.
- **`SAT_LOOP_END`** advances to the next object in the set and jumps back to the entry after the
  `SAT_LOOP_FOR`; when the set is exhausted it falls through.
- **`SAT_LOOP_BREAK`** abandons the loop early and continues after `SAT_LOOP_END`.

Only one loop can be active at a time; the engine logs an error if a script tries to nest them.
`SFO_CURRENT_LOOPED_OBJECT` is how the loop body addresses "the object we're on this pass" — it is
how a script can, for example, do something to every nearby critter or to each follower in turn.

## The object heartbeat clock (`ai_timeevent`)

Living objects do not run their AI or heartbeat scripts on a fixed global tick. Instead each object
schedules its own next heartbeat, and the interval scales with how far the object is from the
player, so distant crowds cost far less than the critter standing next to you.

The interval (`ai_timeevent_delay`, `ai.c:2998`) is:

```
interval_ms = 250 + 4750 · clamp(dist_to_player, 0, 30) / 30
```

where `dist_to_player` is the tile distance to the nearest player, clamped to 0–30
(`ai_distance_to_nearest_player`, `ai.c:3004`). That gives:

| Distance to player | Heartbeat interval |
| ------------------ | ------------------ |
| 0 tiles (adjacent) | 250 ms             |
| 15 tiles           | ~2.6 s             |
| ≥ 30 tiles         | 5000 ms (5 s)      |

On top of that, when an object is **first** scheduled the engine adds a one-time random jitter of
0–5000 ms (`ai.c:2932`), so a freshly loaded crowd does not all tick on the same frame.

Each heartbeat tick (`ai_timeevent_process`, `ai.c:2859`) does roughly this:

1. On the object's very first tick, run its `SAP_FIRST_HEARTBEAT` script (one-time setup) and let it
   process waypoints / standpoints.
2. If the object is **within 30 tiles of the player** and not in turn-based combat
   (`sub_4AD420`, `ai.c:2940`), run its `SAP_HEARTBEAT` script and then its normal AI step
   (`ai_process`). A heartbeat script that returns "skip default" suppresses the AI step.
3. Reschedule the next tick at the distance-scaled interval above.

So `SAP_FIRST_HEARTBEAT` runs exactly once; `SAP_HEARTBEAT` runs repeatedly but only while the
object is reasonably near the player. This is the engine's economy: an object 40 tiles away still
wakes up every 5 seconds (to reschedule and handle bookkeeping), but its full heartbeat-script + AI
work is gated behind the 30-tile check.

## Self-gating NPCs

The heartbeat is also a population-control mechanism. Many maps **place more NPCs in the data than
should appear**, and rely on each one's heartbeat script to decide, at load time, whether it belongs
in this particular playthrough.

The pattern: an NPC is placed in the map, and its `SAP_FIRST_HEARTBEAT` (or early `SAP_HEARTBEAT`)
script checks a **global flag** — typically a quest or "chosen" flag. If the flag says this NPC
should not be here, the script makes the NPC disappear; otherwise it stays. Two common outcomes:

- **Toggle off** — the NPC switches itself out of existence (hidden, non-interactive). This is used
  for an NPC who shouldn't appear because, for instance, the player *is* that character.
- **Kill** — the NPC turns itself into a corpse. Unlike a combat death this awards no experience
  (there is no killer) and leaves a lootable body in place, posed as a static corpse. This is how a
  scene can be pre-populated with the dead (a crash site, a massacre) whose bodies still carry their
  inventories and quest items.

After deciding, such scripts typically remove their own heartbeat slot
(`SAT_REMOVE_THIS_SCRIPT`, `script.c:1535`) so the object stops ticking. The practical consequence:
the heartbeat is not optional cosmetic AI — without it firing, every conditionally-present NPC in a
map stays alive and visible, and the area looks badly overcrowded.

A related, separate case is an NPC that is **stored already dead** in the map data (its
hit-point-damage field set to the engine's "killed" sentinel) rather than killing itself on
heartbeat. Those bodies are lootable from the moment the sector loads and need no script at all.
