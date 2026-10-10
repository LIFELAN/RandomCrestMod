# Changelog

## 0.2.3

- Fixed the sprint dash stab (冲刺斩) always falling back to the Hunter moveset instead of the
  random roll. PlayMaker's `CheckIfCrestEquipped` reads `ToolCrest.IsEquipped`, which Mono is free
  to inline, so the crest spoof never reached the Sprint FSM's branch; that branch is now answered
  directly, and a dash whose attack starts on the same frame the sprint begins is covered too.
- Fixed consecutive sprint / skid binds (滑步缚丝) all reusing the **first** rolled crest until the
  hero stopped, which looked like the same action repeating. The next bind now re-rolls every time.
  The `TickDash` guard that keeps the crest from being dropped mid-bind is **kept**, so the Shaman
  water / Rage-Reaper fixes stay intact.
- New HUD frame appear / disappear animation. The Chaos frame now grows out of the spool and
  retracts back in step with the game's own frame transition (`FrameAppear` / `FrameDisappear`),
  instead of snapping in. Protrusions (top spike, bottom spike, needle) finish together rather than
  the needle lagging behind. On scene load / save load it shows instantly so it does not fight the
  game's own HUD intro.
- Switching between Chaos and the base (un-upgraded) Hunter crest now plays the full transition
  (old frame out, new frame in, change sound). Both share the game's `defaultFrameAnims`, so the
  game used to treat it as "no change" and skip everything.
- **New config options**, all three sharing the unnamed section of
  `BepInEx/config/io.github.lifelan.randomcrestmod.cfg`:
  - `CursedBind` (default `true`): turn the random cursed / refused bind (诅咒缚丝) on or off.
  - `ParryAlwaysSucceed` (default `false`): `false` keeps the Cross Stitch auto counter at its
    **50%** success roll, `true` makes it always land. Real parries are unaffected.
  - `ToolUsesPerBench` (default `16`): moved next to the two new options. It used to live under
    `[Tools]`; if you had customised it, set it again.
- The red tool slot on the crest-selection screen moved up slightly (`0.9` → `0.95`).

## 0.2.2

- The random bind can now resolve to the Cursed crest's **refused bind** (诅咒缚丝). It is a
  fixed **5%** chance and runs through the Bind FSM's
  own cursed branch, so every normal gate (full silk, `CanBind`, ground / sprint / cutscene) still
  applies. The refused bind takes **all** of the hero's silk, and each silk chunk taken now pays
  back **10 rosaries**.
- **Tool barrage is now gated on the Hornet Statuette** (大黄蜂雕像): without the statuette a throw
  is exactly vanilla (single; Quick Sling keeps its vanilla double throw). While it is held the
  old rule applies — **+1** throw per Tool Pouch upgrade and **+1** more with Quick Sling.
- Two new ways to obtain the Hornet Statuette besides the vanilla Fixer quest:
  - a **one-time world pickup in Bellhart (钟心镇)**, in the map-seller area by the entrance. It is
    taken once and gone for good (persisted per save), and is **not** gated on the Chaos crest.
  - while Chaos is equipped, dealing real damage with the **Needle Phial** (储液针管) awards **one**
    statuette **per use** (several damage instances in a single use still count once; more uses
    stack, with no cap).
- The Cross Stitch auto counter now only **lands its follow-up damage 50% of the time**. The
  retreat / highlight always plays; a miss just recovers afterwards. A real parry is unchanged and
  always counters.
- Each **Tool Pouch upgrade** made **while Chaos is equipped** now immediately grants **800 shell
  shards** (clamped by the game's shard cap). Upgrades made on other crests do not count.
- **Rune Rage** (符文之怒 / Silk Bomb) now deals **double damage** while Chaos is equipped. The extra
  multiplier is re-applied after the blast's own `HeroShamanRuneEffect` refresh, so the vanilla
  Zap-rune bonus still stacks on top.

## 0.2.1

- The Chaos crest's HUD frame now turns blue in the blue-health (lifeblood) state, matching every
  vanilla crest. It used to stay silver because the custom frame never mirrored the game's
  lifeblood tint.
- Replaced the HUD frame art. The old gamma-brightened version (commit `c20c9fd`) read as a bright
  silver, which made the blue-health frame look like the Steel Soul HUD; the frame is now the
  new high-resolution art, aligned to the vanilla cloakless spool disk.
- Every red tool is now **always** in the random tool pool, with no config toggle: the Delver's
  Drill, Snare Setter, Needle Phial and Rosary Cannon can all be thrown at random. The Needle Phial
  still works normally while equipped so its quest can be completed (and keeps its own HUD icon),
  while the Snare Setter now follows the random rule like every other tool.
- The Rosary Cannon is kept charged and now counts as equipped while its custom-usage loop runs, so
  holding the button fires continuously just like the vanilla cannon.

## 0.2.0

- Random silk skills except the Cross Stitch now have a **3% per collected silk skill** chance to
  refund the silk their cast spent. Only the first silk spend of a cast rolls, the refund is paid
  immediately (same spool animation as the Cross Stitch refund), and the skill still has to be
  affordable, so silk is required up front. The Cross Stitch keeps its guaranteed real-parry refund.
- The Tool Pouch no longer grants the free-throw chance. Instead every **collected red tool** adds
  **2%** (the Curvesickle upgrade adds another **2%**, capped at 40%) that a throw costs no use. The
  Snare Setter and the Extractor never count, as they are never thrown at random.
- The Snare Setter and the Extractor now keep their vanilla behaviour when equipped on the Chaos
  crest instead of being swapped for a random tool, so their quests / special usage still work (and
  their own HUD icon is shown).
- The random Plasmium Phial now grants **3** blue health instead of 1 once the Plasmium Gland has
  been obtained, so the lifeblood questline pays off on the Chaos crest.
- The random tool HUD icon now turns purple while Poison Pouch poisons the equipped tool. It used
  to go grey because the mod forced the icon white on top of the vanilla poison recolour shader.

## 0.1.9

- Cross Stitch: a real parry (the defensive stance is struck) now refunds the silk the cast spent,
  so a successful block costs nothing. The auto counter (the stance simply expires) still costs its
  silk. Only while the Chaos crest is equipped; every other crest keeps the vanilla parry.
- Optimized the crest textures: a cleaner crest icon and silhouette, plus the Chaos crest's own
  equipment glow (`crest_glow.png`). Selecting / confirming the crest and the skill-get prompt now
  use the crest's own glow and silhouette instead of flashing a Hunter-shaped one.
- The standard taunt flavour can now roll 0 rosaries (the range is 0-50 instead of 1-50) when
  offering shell shards. This means you can now spend shards and gain no rosaries at all.

## 0.1.7

- Random tools can now throw a barrage: one press throws several times (one extra per Tool Pouch
  upgrade, plus one for Quick Sling) and every chained throw re-rolls a fresh random tool.
- The Tool Pouch upgrade now matters on the Chaos crest: shared tool capacity grows by 25% per
  upgrade, and each upgrade adds an 8% chance (capped at 40%) that a throw costs no use.
- Shared tool capacity now starts at 16 uses per bench (was 20) before the Tool Pouch bonus.
- The random taunt is now weighted: standard 80%, Beast 14%, Shakra Ring 6%. The ring check is also
  hardened so a genuinely equipped ring can no longer hijack a standard / Beast roll.
- Completing a taunt on the ground while the Chaos crest is equipped now spends 80 shell shards and
  grants rosaries based on the rolled flavour: standard 1-50, Beast 60, Shakra Ring 80. With fewer
  than 80 shards the taunt stays purely cosmetic.
- Cross Stitch: a real parry keeps the vanilla hit spark again; only the automatic counter removes
  it and uses the pink-white highlight.
- Fixed a Shaman air bind sometimes restoring the crest config while still airborne and stranding
  the hero.

## 0.1.6

- The Cross Stitch (十字绣) now counters on its own: after the defensive stance the hero flows
  straight into the counter even when not struck. Only while the Chaos crest is equipped; every
  other crest keeps the vanilla parry.
- The Cross Stitch hit spark (`Parry Clash Effect`) no longer appears while the Chaos crest is
  equipped; the clash animation, audio and camera shake are unchanged.
- Added a pink-white highlight on Hornet for the Cross Stitch body-recoil step (Chaos crest only).
- Removed the Delver's Drill (掘洞钻 / `Screw Attack`) from the random tool pool: its downward dive
  does not work when thrown at random.

## 0.1.5

- Fixed a random Shaman bind getting stuck on the edge of a top/bottom scene gate when falling into
  it: `TransitionPoint` tests `PlayerData.CurrentCrestID` directly, so the Chaos crest's spoofed
  Shaman bind was rejected and repeatedly pushed out of the gate while the bind kept diving.

## 0.1.4

- The Voltvessels (电枢球) now rolls randomly between its thrown bola and staked spear forms every
  time it is picked. The player's own form setting is restored afterwards, so the save is untouched.
- Hornet's taunt (R3 / V) now randomly plays one of its three vanilla flavours: the standard
  flourish, the Beast crest battle-cry, or the Shakra Ring (投掷环) toss. The Beast flavour also
  enables the Beast crest's unique taunt visual, so the animation matches the voice.
- Removed Rosary Cannon (念珠炮) from the random tool pool: its usage differs and it misfires under
  rapid tool use.

## 0.1.3

- The crest upgrader (伊娃 / Eva) no longer counts the Chaos crest's slots, so her awakening /
  upgrade stages progress exactly like vanilla.

## 0.1.2

- The Flea Charm now stacks with the Chaos silk discount (spells cost 2 silk at full health).
- Random attacks now cover normal / up / down slashes during sprint, air-sprint and skid.
- Fixed the HUD frame getting stuck hidden after switching crests.
- Fixed a random Shaman bind falling through surface water and dropping out of the scene.
- The Chaos crest no longer counts toward the game's completion percentage.
- Removed the cursed-bind feature and all of its workarounds.

## 0.1.1

- Custom HUD frame, random tool / spell icons and save-selection crest spool.
- Renamed the crest to 纷乱 (Chaos) and added a bilingual README.

## 0.1.0

- Initial release: the Chaos crest with random attacks, binds, tools and spells.
