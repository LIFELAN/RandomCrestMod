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
| Tool barrage | **Requires the Hornet Statuette**: one press throws several times — **+1** throw per Tool Pouch upgrade, **+1** more with Quick Sling; every chained throw re-rolls a fresh random tool. **Without the statuette throws are exactly vanilla** (single; Quick Sling keeps its vanilla double throw). |
| Random spells | Every cast silk skill becomes one of the six silk skills at random. Every skill except the Cross Stitch has a **3% per collected silk skill** chance to **immediately refund** the silk its cast spent (same spool animation as the Cross Stitch); the Cross Stitch keeps its guaranteed real-parry refund. |
| Random taunt | Pressing the taunt button (R3 / V) plays a random one of the three vanilla flavours: standard **80%**, Beast crest battle-cry **14%**, or the Shakra Ring toss **6%**. The Beast flavour also switches in the Beast crest's unique taunt visual. |
| Taunt offering | A taunt that plays fully on the ground while Chaos is equipped spends 80 shell shards and grants rosaries based on the rolled flavour: standard 0-50, Beast 60, Shakra Ring 80. With fewer than 80 shards the taunt stays purely cosmetic. |
| Cross Stitch | While Chaos is equipped the Cross Stitch counters on its own. The automatic counter's retreat / highlight **always plays**, but it only **lands its follow-up damage 50% of the time**; a miss just recovers after the highlight. A **real parry always lands** and keeps the vanilla spark. Other crests keep the vanilla parry. |
| Cursed bind | While Chaos is equipped, a random bind has a **5%** chance to become a **refused bind**: it takes **all silk**, and each silk chunk taken pays back **10 rosaries**. Other crests' binds are untouched. |
| Hornet Statuette | The **multi-throw switch**. Besides the vanilla route (Fixer quest) there are two new ways to get it: (1) a one-time world pickup in **Bellhart**, in the map-seller area by the entrance — taken once and gone forever, not crest-gated; (2) while Chaos is equipped, dealing real damage with a **randomly rolled Needle Phial** awards **1 per use** (multi-hit in one use still counts once; more uses stack, **no cap**). A deliberately equipped Needle Phial does **not** award. The statuette can still be consumed for shards — doing so turns multi-throw off. |
| Tool budget | Tools share a base **16 uses per bench** (configurable), **+25%** per Tool Pouch upgrade, and refill for **free**; every **collected** red tool gives a **+2%** chance (the Curvesickle upgrade adds another **+2%**, capped at 40%) that a throw costs no use. The Snare Setter and the Extractor never count. |
| Plasmium Phial | When the random tool is the **Plasmium Phial** and the player owns the **Plasmium Gland**, its blue health is raised from **1 to 3** (no cooldown). |
| Rune Rage | While Chaos is equipped, **Rune Rage** (Silk Bomb) blasts deal **double damage**. |
| Custom HUD | A custom bind-orb frame, fixed "random" tool / spell HUD icons, and a custom crest spool on the save-selection screen. |
| Tool Pouch shards | While Chaos is equipped, each **Tool Pouch upgrade** immediately grants **800 shell shards** (clamped by the game's shard cap; the cap itself rises 25% per Tool Pouch upgrade). Upgrades made on other crests do not count. |

### Special tool handling

- **Every red tool is always in the random pool** — there is no exclusion list and no toggle.
  The **Needle Phial** (`Extractor`) keeps its vanilla behaviour while equipped (it is not swapped
  for a random tool) so its quest can be completed and its own HUD icon is shown; the **Snare
  Setter** (`Silk Snare`) now follows the random rule like every other red tool (it is swapped for a
  random tool while equipped and shows the random HUD icon).
- `Lightning Rod` (**Voltvessels**) rolls randomly between its thrown **bola** and staked **spear**
  forms on every pick; the player's own form setting is restored afterwards.
- `Rosary Cannon` is kept **fully charged** and counts as equipped while its custom-usage loop runs,
  so holding the button fires continuously like the vanilla cannon.

---

## Configuration

Config file: `BepInEx/config/io.github.lifelan.randomcrestmod.cfg`

| Key | Default | Meaning |
| --- | --- | --- |
| `Tools/ToolUsesPerBench` | `16` | Base number of uses every tool is refilled to at a bench (each Tool Pouch upgrade adds 25%). |

> Everything else is **hardcoded** (tuned during development): random attacks / binds / tools /
> spells / taunt / Cross Stitch / spell silk refund / Plasmium Phial boost / Rune Rage damage
> doubling are always on and only
> apply to the Chaos crest; the Cross Stitch auto counter lands exactly **50%** of the time; tool
> barrage needs the **Hornet Statuette**; the refused bind has a fixed **5%** chance and pays a
> fixed **10 rosaries per silk chunk**; every red tool is always in the random pool (the Needle Phial keeps its
> vanilla behaviour while equipped); Voltvessels rolls both of its forms; Rosary Cannon is kept
> fully charged and fires continuously while held; the custom HUD frame, save spool and random
> icons are always on, and the HUD frame offset/scale are fixed too.

---

## Notes

- Random effects only trigger while Chaos is equipped; other crests are completely untouched (tool
  counts, refill costs and tool forms are never modified for them).
- The random tool pool includes tools you have **not obtained**; random spells are all **6** silk
  skills.
- A random bind requires a **full spool (9 silk)**; with less silk it behaves like the vanilla
  "not enough silk" message.
- **Tool barrage** needs the **Hornet Statuette** in the inventory; without it tool throws behave
  exactly like vanilla.
- The Cross Stitch auto counter only lands its follow-up damage **50%** of the time; a miss plays
  the retreat highlight and then recovers.
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
