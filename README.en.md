# RandomCrestMod

[简体中文](https://github.com/LIFELAN/RandomCrestMod/blob/master/README.md) | **English**

> Published on Thunderstore (community **Hollow Knight: Silksong**). The recommended way to install is a
> mod manager (r2modman / Gale / Thunderstore Mod Manager): search for **RandomCrestMod** and it will pull
> in the BepInEx dependency for you.

A **Hollow Knight: Silksong** BepInEx mod that adds a new crest — **Chaos** (纷乱).
Equip it and attacks, binds, tools and spells all become random: **you never know what will happen
next**.

The crest is **unlocked from the start** for every save, can be **switched at any bench**, and has
**all six slots unlocked**. Every random effect only runs **while Chaos is equipped**, so other
crests are completely untouched.

- Mod id: `io.github.lifelan.randomcrestmod`
- Requires: BepInExPack Silksong
- Config file: `BepInEx/config/io.github.lifelan.randomcrestmod.cfg`

---

## Installation

1. Install **BepInExPack Silksong** (a mod manager does this for you).
2. Put `RandomCrestMod.dll` in:

   ```
   <game>/BepInEx/plugins/lifelan-RandomCrestMod/RandomCrestMod.dll
   ```

   (Installed with a mod manager, or build it yourself with `dotnet build -c Release` — the build
   copies the plugin automatically.)
3. Launch the game and pick the **Chaos** crest at any bench.

---

## The crest

| | |
| --- | --- |
| Name | **Chaos** in English, **纷乱** in Chinese |
| Description | You never know what will happen next. |
| Unlock | **Unlocked from the start** for every save |
| Switch | **At any bench**, like a normal crest |
| Slots | **All six unlocked** (1 Red / 1 Skill / 2 Blue / 2 Yellow) |

---

## Features

Everything below only happens **while Chaos is equipped**. Unequip it and the game behaves exactly
like vanilla for every other crest.

| Feature | Behaviour |
| --- | --- |
| Random attacks | Every attack uses a random crest's attack moveset. The direction you pressed (normal / up / down / wall / dash / charge slash) is preserved. |
| Random bind | Every bind uses a random crest's bind effect. |
| Random tools | Every thrown tool becomes a random Red tool from the whole game — obtained or not. |
| Random spells | Every cast silk skill becomes one of the six silk skills at random. |
| Random taunt | Pressing the taunt button (R3 / V) plays a random one of the three vanilla flavours: standard, Beast crest battle-cry, or the Shakra Ring toss. The Beast flavour also switches in the Beast crest's unique taunt visual. |
| Tool budget | Tools share **20 uses per bench** (configurable) and refill for **free**. |
| Custom HUD | A custom bind-orb frame, fixed "random" tool / spell HUD icons, and a custom crest spool on the save-selection screen. |

### Special tool handling

- `Extractor` (**Needle Phial**), `Silk Snare` (**Snare Setter**) and `Rosary Cannon` are excluded
  from the random tool pool — the first two do not work when thrown at random, and the Rosary
  Cannon's usage differs and it misfires under rapid tool use.
- `Lightning Rod` (**Voltvessels**) rolls randomly between its thrown **bola** and staked **spear**
  forms on every pick; the player's own form setting is restored afterwards.
- `Rosary Cannon` is kept **fully charged**, so it always uses its charged form.

---

## Configuration

Config file: `BepInEx/config/io.github.lifelan.randomcrestmod.cfg`

| Key | Default | Meaning |
| --- | --- | --- |
| `Tools/ToolUsesPerBench` | `20` | Number of uses every tool is refilled to when resting at a bench. |

> Everything else is **hardcoded** (tuned during development): random attacks / binds / tools /
> spells are always on and only apply to the Chaos crest; the excluded tools are fixed to Needle
> Phial and Snare Setter; Voltvessels rolls both of its forms; Rosary Cannon is kept fully
> charged; the custom HUD frame, save spool and random icons are always on, and the HUD frame
> offset/scale are fixed too.

---

## Notes

- Random effects only trigger while Chaos is equipped; other crests are completely untouched (tool
  counts, refill costs and tool forms are never modified for them).
- The random tool pool includes tools you have **not obtained**; random spells are all **6** silk
  skills.
- A random bind requires a **full spool (9 silk)**; with less silk it behaves like the vanilla
  "not enough silk" message.
- The Chaos crest does **not** count toward the game's completion percentage (other crests, tools
  and skills are unaffected).

---

## Build

```sh
dotnet build -c Release
```

The build copies `RandomCrestMod.dll` into `BepInEx/plugins/lifelan-RandomCrestMod/`.
Custom art lives in `Assets/` (crest icons, HUD frame, save spool, random icons, ...) and is embedded
into the DLL.

The game path is configured in `SilksongPath.props`.
