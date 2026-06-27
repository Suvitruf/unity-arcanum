# Combat & characters

How the original *Arcanum: Of Steamworks and Magick Obscura* models a character and resolves an attack —
the stat/skill enums, the to-hit and damage math, armour class and resistances, the "damage-taken" HP model,
the effect layer, and level-up. This is a description of **the retail engine**, reconstructed from the
[`arcanum-ce`](https://github.com/alexbatalov/arcanum-ce) decompilation; source references point at
`src/game/<file>:<line>` so each claim can be checked.

**The short version:**

- An attack succeeds when **`difficulty + d100 ≤ effectiveness`** — your skill-based success rate versus an
  accumulated penalty pile that includes the target's armour class.
- Damage rolls a weapon range, adds bonuses, then is reduced by the matching **resistance**. There are **five
  damage types** but the resistance enum lists them **in a different order** (Fire and Poison are swapped), so
  the engine routes each damage type through a small translation table.
- A critter never stores its current HP. It stores **how much damage it has taken** (`OBJ_F_HP_DAMAGE`);
  current HP is always `MaxHp − HpDamage`, and `MaxHp` is recomputed from stats on demand. Death is
  `HpDamage ≥ MaxHp`.
- Stats are one flat enum: eight primary stats (0–7), then derived stats computed from them (carry weight,
  damage bonus, AC adjustment, speed…), with `STAT_LEVEL` at index 17.

---

## Contents

- [The HP model — damage taken, not current HP](#the-hp-model--damage-taken-not-current-hp)
- [Stats](#stats)
- [Skills](#skills)
- [To-hit](#to-hit)
- [Critical hits and misses](#critical-hits-and-misses)
- [Damage, types and resistances](#damage-types-and-resistances)
- [Armour class](#armour-class)
- [Effects](#effects)
- [Level-up](#level-up)

---

## The HP model — damage taken, not current HP

Arcanum **never stores a critter's current HP**. It stores the field `OBJ_F_HP_DAMAGE` — the *amount of damage
accumulated* — and derives the rest:

```
MaxHp        = object_hp_max(obj)            // computed from stats, see below
CurrentHp    = MaxHp − HpDamage              // object.c:1646
Dead         ⇔ CurrentHp ≤ 0  ⇔  HpDamage ≥ MaxHp   // critter_is_dead, critter.c:608
```

`MaxHp` is recomputed from stats every time it is read (`object_hp_max`, `object.c:1632`):

```
MaxHp = HpAdj + 4·HpPts + ( WP + 2·(STR + LEVEL) + 4 )
        └─ object_hp_adj_get  └─ 4·hp_pts   └─────────── sub_43D630 (object.c:1657) ──────┘
```

The whole thing is then passed through the effect layer (`effect_adjust_max_hit_points`) so buffs/items can
move it. Fatigue mirrors this exactly: `OBJ_F_CRITTER_FATIGUE_DAMAGE` is the wound, and
`MaxFatigue = FatiguePts·4 + FatigueAdj + 2·(LEVEL + CON) + WP + 4` (`critter_fatigue_max`, `critter.c:502`),
with current = max − damage (`critter.c:531`).

**Why store the wound and not the value.** `MaxHp` is derived and changes constantly — every level-up, every
+CON item, every expiring buff shifts it. Storing *damage* means current HP auto-tracks: raise `MaxHp` by 10
and current HP rises by 10 while the injury stays exactly as deep as it was. It also means a `MaxHp` drop
can't silently kill you, because death is checked on a damage/kill event, not on a passive recompute.

**Unconsciousness** is the fatigue analogue of death, but only for the living: a critter is unconscious when it
is **not undead** and its current fatigue is ≤ 0 (`critter_is_unconscious`, `critter.c:583`). Undead never
fall unconscious.

### The 32000 kill sentinel

To kill a critter the engine doesn't compute its `MaxHp` — it simply sets `HpDamage = 32000`
(`critter_kill`, `critter.c:634`; the death path does the same at `critter.c:751`). 32000 is larger than any
critter's possible `MaxHp`, so the corpse reads dead regardless of its stat-derived maximum. This is also how
authored corpses (e.g. pre-placed dead bodies) are baked into a map: give them `HpDamage = 32000` and they
spawn dead without the toolset needing to know their stats.

---

## Stats

Every numeric attribute on a critter — primary, derived, and bookkeeping — lives in one flat `Stat` enum
(`stat.h:9`). The **base** values for the primary stats are stored in the array field
`OBJ_F_CRITTER_STAT_BASE_IDX` (`stat.c:613`). Everything else is either derived from those or stored as its
own field.

| Idx | Stat | Idx | Stat |
|----:|------|----:|------|
| 0 | STRENGTH | 14 | REACTION_MODIFIER |
| 1 | DEXTERITY | 15 | MAX_FOLLOWERS |
| 2 | CONSTITUTION | 16 | MAGICK_TECH_APTITUDE |
| 3 | BEAUTY | 17 | **LEVEL** |
| 4 | INTELLIGENCE | 18 | EXPERIENCE_POINTS |
| 5 | PERCEPTION | 19 | ALIGNMENT |
| 6 | WILLPOWER | 20 | FATE_POINTS |
| 7 | CHARISMA | 21 | UNSPENT_POINTS |
| 8 | CARRY_WEIGHT | 22 | MAGICK_POINTS |
| 9 | DAMAGE_BONUS | 23 | TECH_POINTS |
| 10 | AC_ADJUSTMENT | 24 | POISON_LEVEL |
| 11 | SPEED | 25 | AGE |
| 12 | HEAL_RATE | 26 | GENDER |
| 13 | POISON_RECOVERY | 27 | RACE |

The **eight primary stats are 0–7** (STR, DEX, CON, BEAUTY, INT, PERCEPTION, WILLPOWER, CHARISMA — note BEAUTY
at 3 is distinct from CHARISMA at 7). **Derived stats are 8–16**, computed from the primaries. `STAT_LEVEL` sits
at **17**.

### Effective stat = base + adjustments

Reading a stat (`stat_level_get`, `stat.c:336`) is: take the base value, apply situational modifiers
(background traits, a poison penalty to STR/DEX, Tempus Fugit, …), run it through the effect layer
(`effect_adjust_stat_level`), then clamp to the stat's min/max. A primary stat at effective level ≥ 20 is
**extraordinary** and triggers special-case bonuses below.

### Derived stat formulas

All computed in `stat_base_get` (`stat.c:554`); `X` means the effective value of stat `X`.

| Derived stat | Formula | Source |
|---|---|---|
| CARRY_WEIGHT | `500 · STR` (300–10000) | stat.c:558 |
| DAMAGE_BONUS | `STR − 10`; if < 0 halved; doubled if STR extraordinary | stat.c:560 |
| AC_ADJUSTMENT | `DEX − 10` | stat.c:572 |
| SPEED | `DEX`; +5 if DEX extraordinary | stat.c:575 |
| HEAL_RATE | `(CON + 1) / 3` | stat.c:584 |
| POISON_RECOVERY | `CON` | stat.c:587 |
| REACTION_MODIFIER | table lookup on BEAUTY (−65…+75); `2·(5·BEAUTY − 50)` if extraordinary | stat.c:590 |
| MAX_FOLLOWERS | `CHARISMA / 4` (+1 with expert Persuasion) | stat.c:601 |
| MAGICK_TECH_APTITUDE | `(50·MagickPts − 55·TechPts) / 10` + sector adjustment | stat.c:604 |

`STAT_DAMAGE_BONUS` and `STAT_AC_ADJUSTMENT` are the two that feed combat directly — the first adds to melee
damage, the second to armour class.

---

## Skills

There are two skill families. **Basic skills** are the broadly-available ones; **tech skills** are the
technological tree. Each skill has one governing primary stat, and a skill's effectiveness is driven by its
**skill level** (`= 4 × points invested`, capped per governing-stat level).

**Basic skills** (`skill.h:8`), governing stats from `basic_skill_stats[]` (`skill.c:37`):

| Skill | Governing stat | Skill | Governing stat |
|---|---|---|---|
| BOW | DEXTERITY | SPOT_TRAP | PERCEPTION |
| DODGE | DEXTERITY | GAMBLING | INTELLIGENCE |
| MELEE | DEXTERITY | HAGGLE | WILLPOWER |
| THROWING | DEXTERITY | HEAL | INTELLIGENCE |
| BACKSTAB | DEXTERITY | PERSUASION | CHARISMA |
| PICK_POCKET | DEXTERITY | | |
| PROWLING | PERCEPTION | | |

**Tech skills** (`skill.h:26`), governing stats from `tech_skill_stats[]` (`skill.c:57`):

| Skill | Governing stat |
|---|---|
| REPAIR | INTELLIGENCE |
| FIREARMS | PERCEPTION |
| PICK_LOCKS | DEXTERITY |
| DISARM_TRAPS | PERCEPTION |

**Cost.** Each *skill point* costs exactly **1 character point** to buy (`basic_skill_cost_inc`/
`tech_skill_cost_inc` both return 1; `skill.c:909`, `skill.c:1351`), and each point raises the skill's base
level by 4. (Separately, an NPC charging coin to train a skill in dialog uses `(training+1)·(skillLevel+2)`,
`skill.c:763` — that is a service price, not a character-point cost.)

---

## To-hit

A skill check — including every attack — succeeds when (`skill.c:1569`):

```
HIT  ⇔  difficulty + d100 ≤ effectiveness
```

(`d100 = random_between(1,100)`; a "forced" invocation always succeeds.)

### Effectiveness — your success rate

`effectiveness` is a skill's success rate, driven by its skill level (`basic_skill_effectiveness`,
`skill.c:771`; `tech_skill_effectiveness`, `skill.c:1284`). The combat-relevant bases:

| Skill | Effectiveness |
|---|---|
| MELEE, BOW | `5·lvl + 25` |
| THROWING | `7·lvl + 25` |
| FIREARMS | `5·lvl + 25` |
| DODGE | `5·lvl` |
| HEAL | `5·lvl` |

Then: **+10 if the character's INTELLIGENCE is extraordinary** (`skill.c:821` — note it keys off INT for every
skill, not the governing stat); a PC game-difficulty scale (Easy `+½`, Hard `−¼`); and a **clamp to 95** only
for skills flagged for it — DODGE is clamped, but BOW/MELEE/THROWING/FIREARMS are **not**.

### Difficulty — the penalty pile

`difficulty` starts at the caller's modifier and accumulates (`skill_invocation_difficulty`, `skill.c:2210`):

| Term | Effect | Source |
|---|---|---|
| Weapon to-hit bonus | `− toHit` (weapon bonus + magic) | skill.c:2265 |
| Too weak for weapon | `+ 5·(minStr − STR)` | skill.c:2269 |
| **Target armour class** | `+ effectiveness · (AC/2) / 100` | skill.c:2328 |
| Called shot | `− hitLocPenalty` (head/arm/leg, see below) | skill.c:2312 |
| Out of range | `+ 1000000` | skill.c:2357 |
| Long shot | `+ 5·(dist − PER/2)` | skill.c:2370 |
| Blocked line of fire | `+ 1000000` | skill.c:2382 |
| Darkness | up to `+30 · (255 − light)/255` | skill.c:2425 |
| Target paralyzed/unconscious/asleep | `− 50` | skill.c:2290 |
| Target stunned/knocked-down | `− 30` | skill.c:2295 |
| Target unaware | `− 30` | skill.c:2303 |
| Attacker blinded | `+ 30` | skill.c:2459 |
| Attacker one arm / both arms crippled | `+ 20 / + 50` | skill.c:2466 |
| Attacker both legs crippled | `+ 30` | skill.c:2477 |

The **AC term is the only place armour class enters the to-hit roll** — note it scales with the attacker's own
effectiveness, so a more skilled attacker is penalised *more* (in absolute points) by the same AC, but still
hits more often overall.

---

## Critical hits and misses

After a hit/miss is determined, two further rolls decide whether it was *critical*. Both compare a d100 against
a chance derived from effectiveness, with combat skills using a much harsher divisor than non-combat ones.

**Critical hit** (`skill_invocation_check_crit_hit`, `skill.c:2066`):

```
chance = effectiveness / 20      (combat skills;  / 2 non-combat)
       + |calledShotPenalty| / 5
       + weapon magic crit bonus
       + (2·backstabLevel − targetLevel)   [+20 if backstab master]
CRIT  ⇔  d100 ≤ chance
```

**Critical miss / fumble** (`skill_invocation_check_crit_miss`, `skill.c:2131`):

```
chance = (100 − effectiveness) / 7   (combat skills;  / 2 non-combat)
       + damaged-weapon%
       + weapon magic fumble bonus
FUMBLE ⇔  d100 ≤ max(chance, 2)      // floor of 2%
```

(A Melee Master never fumbles — gated in the caller, `skill.c:1580`.)

The *outcome* of a critical is computed procedurally, not from a static table. A crit hit
(`combat_process_crit_hit`, `combat.c:2327`) first rolls a bonus-damage tier — `+200%`, `+100%`, or `+50%`
depending on how far under the chance the roll lands (`combat.c:2381`) — then rolls hit-location/body-type
gated effects: stun, knockout, knockdown, cripple an arm or leg, blind, knock off a helmet or weapon, damage
weapon or armour. A crit miss (`combat.c:2503`) can damage the attacker's own weapon/ammo and has a 50% chance
to escalate into a full self-inflicted critical.

**Hit location** is rolled when the attacker isn't aiming at the torso (`combat_random_hit_loc`,
`combat.c:2564`):

| Location | Chance | Called-shot to-hit penalty |
|---|---:|---:|
| Torso | 70% | 0 |
| Leg | 15% | −30 |
| Arm | 10% | −30 |
| Head | 5% | −50 |

(The called-shot penalties are `hit_loc_penalties`, `combat.c:77`. Penalties are negative numbers added to
the difficulty pile, so they make the shot harder while the called shot raises crit chance.)

---

## Damage, types and resistances

### The five damage types

```
DAMAGE_TYPE = { Normal=0, Poison=1, Electrical=2, Fire=3, Fatigue=4 }   // damage_type.h:6
```

Fatigue ("subdual") damage fills the fatigue pool rather than the HP pool, knocking a target out instead of
killing it.

### The damage formula

For each damage type the engine rolls the weapon's range and adds bonuses (`combat_calc_dmg`,
`combat.c:2607`; ranges from `item_weapon_damage`, `item.c:3399`):

```
dam = random_between(weaponMin[type], weaponMax[type])
    + STAT_DAMAGE_BONUS         // melee, on the Normal/Fatigue components only (item.c:3462)
    + backstab bonus            // Normal only: +5·level (master) or +level (item.c:2644)
    + weapon magic adjustment[type]
```

Unarmed combat synthesises a range: `max = unarmedDamage + 5`, `min = unarmedDamage − 25` (floored at 1), and
only the Normal and Fatigue components apply (`item.c:3441`). Poison deals nothing to undead, mechanical, or
petrified targets (`combat.c:2636`). A massive-damage cap clamps each component to `3×` its max (`2×` for
unarmed melee).

### Resistance reduction — and the order gotcha

Each component is then reduced by the target's resistance to that type (`combat_apply_resistance`,
`combat.c:2682`):

```
final = dam · (100 − resistance) / 100     // only a positive resistance reduces; no vulnerability
```

The catch is that the **resistance enum is in a different order than the damage enum** — **Fire and Poison are
swapped**, the resistance enum has a *Magic* slot with no matching damage type, and the damage enum has a
*Fatigue* type with no dedicated resistance:

```
RESISTANCE_TYPE = { Normal=0, Fire=1, Electrical=2, Poison=3, Magic=4 }   // resistance.h:4
```

So the engine cannot index resistances with the damage type directly. It routes through
`combat_damage_to_resistance_tbl` (`combat.c:120`):

| Damage type | → Resistance type |
|---|---|
| Normal (0) | Normal (0) |
| Poison (1) | **Poison (3)** |
| Electrical (2) | Electrical (2) |
| Fire (3) | **Fire (1)** |
| Fatigue (4) | Normal (0) |

Two further rules:

- **Fatigue resistance is taken at ¾.** For fatigue damage the resistance value (which is the *Normal*
  resistance, per the table) is scaled `resistance = 3·resistance/4` before the reduction (`combat.c:2704`).
  The ¾ scales the *resistance*, not the damage.
- **CON boosts poison resistance.** When reading poison resistance the engine adds `5·(CON − 4)`, but only if
  positive (`object_get_resistance`, `object.c:1708`). So a CON above 4 grants free poison resistance.

A target's total resistance is its base `OBJ_F_RESISTANCE_IDX` plus the sum of worn-armour resistance
adjustments plus the CON poison bonus, clamped to **0–95** (`object.c:1723`).

---

## Armour class

AC is the defensive number that feeds the to-hit AC term above. It is assembled in `object_get_ac`
(`object.c:1733`):

```
AC = OBJ_F_AC (base)
   + Σ worn-armour AC adjustments
   + STAT_AC_ADJUSTMENT  (= DEX − 10)
   + AC effect adjustments
   clamped to 0–95
```

So a high DEX directly raises AC, and worn armour stacks on top.

---

## Effects

An **effect** is a bundle of attribute modifiers applied to a critter — from worn items, spells, race,
blessings, curses, poison, injuries, and so on. The cause is one of (`EFFECT_CAUSE_*`, `effect.h:14`):

```
Race, Background, Class, Bless, Curse, Item, Spell, Injury, Tech, Gender
```

Each effect carries up to seven *change entries* — a target attribute id, a magnitude, and an operator (add,
multiply, divide, min-clamp, max-clamp, percent) (`Effect` struct, `effect.c:73`). The active effects on a
critter live in `OBJ_F_CRITTER_EFFECTS_IDX`. When a stat / resistance / AC / max-HP is read, the engine walks
those effects and folds in every matching change (`effect_adjust_func`, `effect.c:742`):

| Adjuster | What it modifies | Source |
|---|---|---|
| `effect_adjust_stat_level` | a stat — including AC, which is the `STAT_AC_ADJUSTMENT` stat | effect.c:732 |
| `effect_adjust_resistance` | a resistance value | effect.c:890 |
| `effect_adjust_max_hit_points` | the `MaxHp` total | effect.c:910 |
| `effect_adjust_max_fatigue` | the `MaxFatigue` total | effect.c:920 |

There is no separate AC adjuster: AC is a stat (`STAT_AC_ADJUSTMENT`), so AC-modifying effects flow through
`effect_adjust_stat_level` like any other stat. Because `MaxHp`/`MaxFatigue` are recomputed through these
adjusters every time they are read, gaining or losing an effect re-derives them automatically — and, since
death is event-driven, a buff expiring can lower `MaxHp` below current damage without instantly killing the
critter.

---

## Level-up

### XP → level thresholds

The XP needed for each level is a table loaded from `rules\xp_level.mes` into `level_xp_tbl[]`
(`level_init`, `level.c:269`): the message numbered `N` holds the cumulative XP required to *be* level `N`
(level 1 forced to 0). XP-to-next is `level_xp_tbl[level] − currentXP` (`level.c:366`). When a critter's
experience changes, `level_recalc` (`level.c:454`) bumps `STAT_LEVEL` while the remaining XP for the next
level is ≤ 0.

### Character points awarded

Each level grants character points (`calculate_bonus_character_points`, `level.c:432`):

```
1 point per level gained, plus 1 extra point on every 5th level
```

So levels 1–5 grant 1,1,1,1,2 = 6 points, and so on. They accumulate in `STAT_UNSPENT_POINTS`.

### Spending points

Both stats and skills cost **exactly one character point per point of increase**:

- **Stats:** `stat_cost` (`stat.c:835`) reads `stat_cost_tbl[]`, which is all 1s (`stat.c:151`). Raising any
  stat from any level to the next costs 1 point. (Stats cap at 20.)
- **Skills:** each skill point costs 1 character point (`skill.c:909`) and raises the skill's base level by 4.

This flat one-point cost is Arcanum's signature character-advancement design: no escalating cost curve — every
improvement, stat or skill, is one of your hard-won points.
