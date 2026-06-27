# Dialog (conversations)

How *Arcanum: Of Steamworks and Magick Obscura* stores NPC conversations and decides what gets shown.
A conversation lives in a plain-text `.dlg` file as a flat list of numbered records; the engine walks
that list, deciding for each record whether it is **something the NPC says** or **an option the player can
pick**, and filtering player options by who the talking PC is (gender, intelligence) and by gated
**condition** codes. Picking an option can jump to another record and run **effect** codes that touch the
world (gold, quests, reaction, party membership, combat). Reverse-engineered from `arcanum-ce src/game/dialog.c`
and `reaction.c`, validated against the shipped `dlg/*.dlg`.

## Contents

- [Which dialog runs](#which-dialog-runs)
- [The `.dlg` record format](#the-dlg-record-format)
- [NPC line vs player option — the IQ rule](#npc-line-vs-player-option)
- [Gender variants & gates](#gender)
- [`@name@` text codes](#name-codes)
- [Condition codes (tests)](#condition-codes)
- [Effect codes (actions)](#effect-codes)
- [Reaction](#reaction)

## Which dialog runs

A critter carries **two** dialog scripts in its script-slot field, each a `.dlg` file:

| Slot | Holds | Entered at |
|---|---|---|
| **`SAP_DIALOG`** | the **main conversation** — what you get when you talk to the NPC | line 1 |
| **`SAP_DIALOG_OVERRIDE`** | a *generated / state* dialog (`*override.dlg`): healing menus, follower join/leave prompts, combat barks, store messages | high-numbered entries (10000+) reached only by jumping in |

The main conversation is the one a normal "talk" starts, and it always begins at **line 1**. The override
file has **no line-1 entry** — its records are high-numbered and are only reached when the engine jumps in
for a specific generated situation, so you cannot start a conversation there. (For example, Virgil's
override file contains only 10000+ entries; trying to open a conversation against it yields no greeting and
no options.)

## The `.dlg` record format

One record per line, encoded in **Latin-1** (Windows-1252). Each field is wrapped in braces, so a record is
seven brace-delimited fields in order (`dialog_parse_field`, `dialog.c:2737`):

```
{num}{text1}{text2}{iq}{test}{goto}{effect}
```

`//` begins a line comment (the rest of the line is skipped). Parsing is done by `dialog_parse_entry`
(`dialog.c:2641`). The meaning of three of the fields depends on whether the record is an NPC line or a
player option (see the IQ rule below):

| Field | NPC line (`iq` blank) | Player option (`iq` non-zero) |
|---|---|---|
| `num` | line number (records are sorted/searched by this) | line number |
| `text1` | speech shown to a **male** PC | the option text the player reads |
| `text2` | speech shown to a **female** PC (blank ⇒ reuse `text1`) | a **gender gate** number (blank = any) |
| `iq` | blank (⇒ 0) | min IQ (`> 0`) or max IQ (`< 0`); must not be 0 |
| `test` | (ignored on NPC lines) | condition string — all codes must pass (AND) |
| `goto` | (the "response value": often the line whose options follow) | line to jump to when the option is chosen |
| `effect` | actions run when the NPC line is reached | actions run when the option is chosen |

The `iq` field is validated at parse time: it must be **blank for an NPC line** or **non-zero for a player
option**. A literal `0` that isn't blank is rejected as an error (`dialog.c:2668`). If an NPC line's
`text1` is non-blank but its `text2` (female) variant is blank, the parser logs a "missing female response"
warning (`dialog.c:2728`) — but still accepts the record and falls back to `text1`.

## NPC line vs player option

**The role of a record is decided purely by `iq`** (`dialog.c:1343`, `2696`):

- `iq == 0` (blank) ⇒ the record is **a line the NPC speaks**.
- `iq != 0` ⇒ the record is **a player option**.

When the conversation is sitting on an NPC line numbered *N*, the player options offered are the
**consecutive higher-numbered records following *N***, stopping at the next NPC line (the option scan in
`dialog.c:1341` returns as soon as it hits an entry with `iq == 0`). At most five options are collected per
turn. If, after filtering, no option survives, the engine substitutes a generic "goodbye/leave" option.

## Gender

There are two distinct uses of gender, both keyed on the **player character's** gender (the NPC is
addressing *you* — "sir" / "madam"), never on the NPC's:

- **NPC speech variant.** When an NPC line is shown, a non-male PC gets `text2` (the female variant) and a
  male PC gets `text1`; a blank `text2` reuses `text1` (`dialog.c:2332`).
- **Option gender gate.** On a player option, `text2` is **not** text — it holds a number that gates the
  option by PC gender (`dialog.c:1347`). The value is parsed at load time: blank ⇒ `-1` meaning "any
  gender"; otherwise it is matched against the PC's gender. An option whose gate doesn't match the PC's
  gender is filtered out. Because `text2` on an option is a gate, it is never displayed.

## Name codes

Speech and option text may contain inline substitution codes delimited by `@` (`dialog.c:2362`):

| Code | Expands to |
|---|---|
| `@pcname@` | the player character's name |
| `@npcname@` | the speaking NPC's name (as the PC would examine it) |

Substitution scans for paired `@…@` markers and replaces recognised codes; an unrecognised code between two
`@` is effectively dropped (only `pcname`/`npcname` are handled), and a stray unmatched `@` is left as-is.

## Condition codes

A player option's `test` field is a list of **2-character codes**, each followed by one or two integer
arguments. **Every** listed condition must pass for the option to be shown (logical AND). Evaluated by
`sub_4150D0` (`dialog.c:1361`); an unknown code causes the option to fail (be hidden).

**Sign convention for comparative codes:** a **positive** argument means "actual value **≥** arg"; a
**negative** argument means "actual value **≤** |arg|". So `ch4` requires Charisma ≥ 4, while `ch-4`
requires Charisma ≤ 4. Codes that aren't comparative (flags, quests, items, race, "in area") use the
argument's sign differently, noted below.

| Code | Tests | Sign / argument behaviour |
|---|---|---|
| `ps` | Persuasion skill | comparative |
| `ch` | Charisma | comparative |
| `pe` | Perception | comparative |
| `al` | Alignment (good ↔ evil axis) | comparative |
| `ma` | Magick aptitude | comparative |
| `ta` | Tech aptitude (negated magick aptitude) | comparative |
| `gv` | Global variable == value | `gv<idx> <value>` equality |
| `gf` | Global flag == value | equality |
| `qu` | Quest state == value | equality |
| `re` | **Reaction** toward this PC | comparative |
| `$$` | Gold (PC + followers) | comparative |
| `in` | Item in inventory | `+`: PC/followers have it; `-`: NPC has it |
| `ha` | Haggle skill | comparative |
| `lf` | Local flag (dialog slot) == value | equality |
| `lc` | Local counter (dialog slot) == value | equality |
| `tr` | Skill training level | comparative |
| `sk` | Skill level | comparative |
| `ru` | Rumor known | `+`: known; `-`: not known |
| `rq` | Rumor quest-state | `+`: set; `-`: not set |
| `fo` | Following — is this NPC in the PC's party | `0` = is leader's follower, `1` = is not |
| `le` | PC level | comparative (note: stored negated) |
| `qb` | Quest state ≤ value ("quest below/at") | `≤` |
| `me` | Met this PC before | `0` = not met, `1` = met |
| `ni` | **Not** in inventory | inverse of `in` |
| `qa` | Quest state ≥ value ("quest at/above") | `≥` |
| `ra` | PC race | `+N` = race is N; `-N` = race is not N |
| `pa` | A specific party member present (by name) | `+` present, `-` absent |
| `ss` | Story state | comparative |
| `wa` | NPC "wait here" flag | `0` / `1` |
| `wt` | NPC "jilted" flag | `0` / `1` |
| `pv` | PC variable == value | equality |
| `pf` | PC flag == value | equality |
| `na` | Alignment, opposite axis to `al` | comparative |
| `ar` | Area known to the PC | `+N` known, `-N` not known |
| `rp` | Reputation held | `+N` has it, `-N` does not |
| `ia` | PC currently in area | `+N` is in area N, `-N` is not |
| `sc` | Spell-college level | comparative |

## Effect codes

A player option's `effect` field (and an NPC line's `effect`) is a list of **2-character action codes**,
each with arguments, run in order when the option is chosen / the line is reached. Evaluated by
`sub_415BA0` (`dialog.c:1862`); an unknown code stops processing the rest of the string.

Three codes — `$$`, `re`, `al` — read a leading **operator** before their value to choose how to apply it
(`dialog.c:1942`, `1988`):

| Operator | Meaning |
|---|---|
| `+` / `-` | adjust by the (signed) amount |
| `>` | clamp **up** to the value (set only if currently lower) |
| `<` | clamp **down** to the value (set only if currently higher) |
| *(none)* | set exactly to the value |

| Code | Action |
|---|---|
| `$$` | Give / take gold (operator modes above; takes from PC then followers) |
| `re` | Adjust **reaction** toward the PC (operator modes above) |
| `qu` | Set a quest's state |
| `fl` | Jump ("follow link") to another line, run its effects, continue there |
| `co` | Start **combat** — NPC attacks the PC (applied after the whole effect string) |
| `gv` / `gf` | Set global variable / global flag |
| `mm` | Mark an area as known on the map |
| `al` | Set / adjust **alignment** (operator modes above) |
| `in` | Transfer an item: `+` NPC→PC, `-` PC→NPC |
| `lf` / `lc` | Set local flag / local counter (dialog slot) |
| `tr` | Set a skill's training level |
| `ru` | Mark a rumor as known to the PC |
| `rq` | Set a rumor quest-state |
| `jo` | NPC **joins the party** (if allowed; else shows a refusal) |
| `wa` | Tell the NPC to wait here |
| `lv` | NPC **leaves** the party (disband) |
| `ss` | Set the global **story state** |
| `sc` / `so` | Party formation: stay close / spread out |
| `uw` | Un-wait — bring a waiting follower back |
| `pv` / `pf` | Set PC variable / PC flag |
| `xp` | Award quest XP (looked up by quest id) |
| `nk` | **Kill** the NPC |
| `rp` | Add / remove a reputation (`+N` add, `-N` remove) |
| `np` | Enqueue a newspaper article |
| `ce` / `su` / `ii` / `ri` | Party-order prompts (combat orders, surrender, etc.) |
| `fp` | Grant the PC a fate point |
| `or` | Set the NPC's origin / faction |
| `et` | Train a skill to **Expert** (gated by current level / training) |

## Reaction

**Reaction** is a 0–100 disposition number an NPC holds toward a particular PC, and it gates which option
variants appear. It is computed by `reaction_get` (`reaction.c:193` → `sub_4C0D00`): the NPC's **base**
disposition plus remembered per-PC adjustments and a few modifiers. Important defaults and clamps
(`reaction.c:215`):

- If the target isn't actually a PC, reaction returns **50**.
- If the speaker isn't actually an NPC, reaction returns **50**.
- If the PC is on the NPC's "shit list", reaction is **0** (hatred), regardless of base.
- The NPC's base disposition comes from its reaction-base object field; the engine treats a missing/neutral
  base as **50 = neutral**.

The base maps to named bands via `reaction_translate` (`reaction.c:318`):

| Reaction value | Band |
|---|---|
| ≤ 0 | Hatred |
| 1–20 | Dislike |
| 21–40 | Suspicious |
| 41–60 | Neutral |
| 61–80 | Courteous |
| 81–100 | Amiable |
| > 100 | Love |

**How it gates dialog.** Options carry reaction conditions via the `re` test code (comparative sign rule):
`re62` shows the option only when reaction ≥ 62; `re-61` only when reaction ≤ 61. A greeting commonly lists
two near-mirror options — one gated `re62`, one gated `re-61` — so the NPC presents a friendlier or curter
set of choices depending on disposition; with a real reaction value only one of the pair passes. The `re`
**effect** code (with `+`/`-`/`>`/`<` operators) is how a conversation nudges that disposition.
