# Magick — Arcanum's spell system & eye-candy data

*Arcanum: Of Steamworks and Magick Obscura* organises its magick into **16 colleges of 5 spell
levels each** — 80 player-castable spells in all — and **casting costs fatigue, not a separate mana
bar**. The visually interesting, reverse-engineered part is the **eye-candy chain**: a three-file
lookup (`mes/spell.mes` → `rules/SpellEyeCandy.mes` → `art/eye_candy/eye_candy.mes`) that turns a
spell number into the actual `.art` sprite files the engine plays while a spell is cast, flies, and
lands.

This describes the **original engine**, reconstructed from the `arcanum-ce` decompilation
(`src/game/magictech.c`, `animfx.c`, `spell.c`, `name.c`) and the shipped data files.

## The 16 colleges

Spells are grouped into 16 colleges, declared in order (`spell.h:6`):

| # | College | # | College |
|---|---|---|---|
| 0 | Conveyance | 8 | Meta |
| 1 | Divination | 9 | Morph |
| 2 | Air | 10 | Nature |
| 3 | Earth | 11 | Necromantic (Black) |
| 4 | Fire | 12 | Necromantic (White) |
| 5 | Water | 13 | Phantasm |
| 6 | Force | 14 | Summoning |
| 7 | Mental | 15 | Temporal |

Each college holds exactly **5 spells**, ordered by level (1–5). The mapping is purely positional
(`spell.h:142`):

```
COLLEGE_FROM_SPELL(spell) = spell / 5
LEVEL_FROM_SPELL(spell)   = spell % 5
```

So spell 0 is Conveyance level 1, spell 4 is Conveyance level 5, spell 5 is Divination level 1, and
so on up to spell 79 (Temporal level 5). The full ordered list of the 80 spell identifiers is in
`spell.h:28` — e.g. Conveyance runs Disarm / Unlocking Cantrip / Unseen Force / Spatial Distortion /
Teleportation; Necromantic White ends at Resurrect; Temporal ends at Tempus Fugit.

> The engine's internal magictech table (`MT_SPELL_COUNT = 223`, `magictech.h:15`) is larger than the
> 80 player spells — it also covers non-player and special effects. The 80 colleges×levels spells are
> the ones a character learns and casts.

A 17th index (`SPELL_MASTERY_IDX`, `spell.h:26`) sits past the 16 colleges and tracks which college,
if any, the character has reached **mastery** in.

## Casting cost is fatigue

**Arcanum has no mana bar for a character's own spells.** Casting drains **fatigue**: the spell's cost
is added to the caster's *fatigue-damage* pool, exactly like taking a hit to stamina
(`magictech.c:1650`):

```
fatigue_dam = critter_fatigue_damage_get(obj);
critter_fatigue_damage_set(obj, fatigue_dam + cost);
```

A cast is permitted right up to the point of **overexertion**. The gate (`magictech.c:1645`) is:

```
if (critter_fatigue_current(obj) - cost <= -15)  → cast fails
```

That is: you may cast as long as the cost would leave current fatigue **above −15**. Fatigue can be
driven *negative* (into the red) by overexerting — a character can keep casting past zero down toward
the −15 floor, at which point further casts are refused. (For maintained spells, an additional check
refuses to start one whenever it would leave the caster's fatigue already below zero,
`magictech.c:1657`.)

Wands, scrolls and other charged items are the exception: they cast from the item's own mana store
rather than the wielder's fatigue.

## The eye-candy (visual-effect) data chain

Every spell can show up to **six** distinct visual effects — one per phase of the cast. The phase is
the **eye-candy type** (`magictech.h:27`):

| Value | Type | When it plays |
|---|---|---|
| 0 | `CASTING` | on the caster as the spell is cast |
| 1 | `PROJECTILE` | the travelling missile, caster → target |
| 2 | `DESTINATION` | at the target where the spell lands |
| 3 | `SECONDARY_DESTINATION` | a second effect at the destination |
| 4 | `SECONDARY_CASTING` | a second effect on the caster |
| 5 | `DAMAGE` | when the spell deals damage |

Resolving a spell phase to a sprite is a **three-file lookup**:

| File | Key | Yields |
|---|---|---|
| `mes/spell.mes` | spell number (used directly) | the spell's display name |
| `rules/SpellEyeCandy.mes` | `spellNumber * 10 + fxType` | an entry string `"Art: N, Palette: …, Scale: …, Blend: …, Sound: …"` |
| `art/eye_candy/eye_candy.mes` | art num `N` | a base name → the sprite path `art/eye_candy/<name>_<F\|B\|U>.art` |

### 1. Spell number → name (`mes/spell.mes`)

The engine reads the name straight off the message file by the spell number — no arithmetic
(`magictech.c:1281`):

```
char* magictech_spell_name(int num) {
    mes_file_entry.num = num;            // key = the spell number itself
    mes_get_msg(magictech_spell_mes_file, &mes_file_entry);
    return mes_file_entry.str;
}
```

### 2. Spell + phase → eye-candy entry (`rules/SpellEyeCandy.mes`)

`rules/SpellEyeCandy.mes` is loaded as an *animfx list* with these parameters (`magictech.c:785`):

```
path       = "Rules\SpellEyeCandy.mes"
num_fields = 6     // six fx types per spell
step       = 10    // each spell's block of keys is 10 apart on disk
```

Because each spell owns a block of keys spaced **10 apart on disk** but only the first **6** are used
(one per fx type), the on-disk key for a given phase is:

```
mesKey = spellNumber * 10 + fxType
```

So spell 5's CASTING entry is key 50, its PROJECTILE entry is key 51, its DESTINATION entry key 52,
and so on; keys 56–59 in that block are unused padding before spell 6's block begins at key 60.
(Internally, after loading, the engine packs these into a dense array indexed `spell * 6 + fxType`,
since only 6 of every 10 slots carry data — see `magictech.c:6193`.)

Each entry is a comma-separated string parsed field-by-field (`animfx.c:1088`):

| Field | Meaning |
|---|---|
| `Art:` | the **eye-candy art number** `N` (required; without it there is no sprite) |
| `Palette:` | palette index applied to the sprite |
| `Scale:` | scale keyword (looked up to a percentage; marks the effect auto-scalable) |
| `Blend:` | blend/translucency mode |
| `Sound:` | sound effect id (if absent, derived from the spell's base sound) |
| `Flags:` | overlay flags — these decide whether the art is an overlay or an underlay |

The `Art: N` value is handed to `tig_art_eye_candy_id_create` (`animfx.c:1145`) to build an
**eye-candy art id**, with the overlay/underlay type chosen from the parsed flags. An entry with no
`Art:` field produces no sprite (it may still carry just a sound).

### 3. Art number → sprite path (`art/eye_candy/eye_candy.mes`)

Finally, resolving an eye-candy art id to a file looks the art **number** up in
`art/eye_candy/eye_candy.mes` to get a base name, then appends an overlay-type suffix
(`name.c:1031`):

```
art\eye_candy\<name>_<F|B|U>.art
```

The suffix is the **overlay type** baked into the art id (`name.c:172`):

| Code | Overlay type |
|---|---|
| `F` | Foreground overlay (drawn above the scene) |
| `B` | Background overlay |
| `U` | Underlay (drawn beneath) |

So a CASTING entry of `Art: 12` with a foreground overlay flag becomes, after the name lookup,
something like `art/eye_candy/spell_cast_glow_F.art`.

## Not every spell has every phase

The six fx slots are a maximum, not a requirement — many entries are simply blank. The important
consequence is **instant and touch spells have no PROJECTILE entry**: nothing flies through the air,
so only the CASTING effect on the caster and the DESTINATION effect on the victim are populated. Harm
(internally "Cause Light Wounds") is the canonical example — it fills CASTING and DESTINATION but
leaves PROJECTILE empty. The engine reflects this directly: it only attempts a missile when a
PROJECTILE entry actually resolves for the spell (`magictech.c:6193`).

## Source references

- Colleges, level math, mastery index — `spell.h:6`, `:26`, `:142`
- Spell identifiers (80 player spells) — `spell.h:28`
- Fatigue cost / overexertion gate — `magictech.c:1644`–`:1660`
- Spell-name lookup (key = number) — `magictech.c:1281`
- Eye-candy list params (`step=10`, `num_fields=6`) — `magictech.c:785`
- Fx-type enum (CASTING…DAMAGE) — `magictech.h:27`
- Entry string parsing (`Art:`/`Palette:`/`Scale:`/`Blend:`/`Sound:`) — `animfx.c:1061`–`:1160`
- Eye-candy art id creation — `animfx.c:1145`
- Art number → `<name>_<F|B|U>.art` path & overlay codes — `name.c:1031`, `:172`
- Missile only when a PROJECTILE entry resolves — `magictech.c:6193`
