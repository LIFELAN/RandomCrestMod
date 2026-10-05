# Changelog

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
