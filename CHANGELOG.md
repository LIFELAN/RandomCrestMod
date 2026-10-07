# Changelog

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
