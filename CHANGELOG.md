# Changelog

Notable changes in each release. The section for a tagged version becomes that release's notes on
GitHub, so keep the headings as `## <version> - <date>`.

## 1.2.1 - 2026-11-02

**No double charge next to ValheimPlus.** With `CraftFromChests`, `BuildFromChests` and
`StationsFromChests` all off, the mod still took a recipe's shortfall out of nearby chests whenever
the game spent materials. ValheimPlus's CraftFromChest does the same, so running both charged the
chests twice (10 wood carried 0 took 20 from the chest). Spending from chests now only happens when
one of those settings is on; with them off the inventory behaves exactly as vanilla. Stack, Tidy and
`PullBuildMaterials` are unaffected.

Thanks to [mats-johanson](https://github.com/mats-johanson).
([#13](https://github.com/timothydodd/valheim-nearby-chests/pull/13))

## 1.2.0 - 2026-10-16

**Choose your chests.** New `OnlyUseMarkedChests` setting, off by default. Turn it on and each
chest's window gets an **Include in NearbyChests** checkbox; only ticked chests are used for
crafting, building, station feeding, Stack and Tidy. Stack or Tidy on an unticked chest shows a
message. The tick is saved on the chest, so other players with the mod see it too.

**Stack and tidy with one key.** New `StackAndTidyKey` setting, unbound by default. With no chest
open, it stacks your inventory and then tidies every chest in stacking range, re-sorting each one.
F16 to F24 work here and for `PullBuildMaterialsKey`, which Valheim's own key handling can't read.

**Keep empty chests empty.** New `DontFillEmptyChests` setting, off by default. Stack and Tidy never
start a new chest; items with no home stay with you.

Thanks to [rattus-ratgirl](https://github.com/rattus-ratgirl) for all three.
([#6](https://github.com/timothydodd/valheim-nearby-chests/pull/6),
[#7](https://github.com/timothydodd/valheim-nearby-chests/pull/7),
[#8](https://github.com/timothydodd/valheim-nearby-chests/pull/8))

## 1.1.2 - 2026-10-12

**Stack and Tidy leave the Obliterator alone.** Pressing Place stacks with the Obliterator open could
send items into it that you never meant to destroy: new materials went into it as if it were an
empty chest, and items from the same group as what you'd put in (Flint with Stone) were sent to it
as their "similar" chest. Stack on the Obliterator or a gravestone now does the vanilla stack, so
only items already inside go in, and the Tidy button is hidden there so it can't pull items in
from your chests.

**Put leftovers anywhere.** New `FallbackToAnyChest` setting, off by default. When an item has no
similar chest, no empty chest and no shared chest, it goes into any nearby chest with room instead
of staying in your inventory. Holding Use on a chest then puts everything away; run Tidy later to
sort out the mix. ([#1](https://github.com/timothydodd/valheim-nearby-chests/issues/1))

## 1.1.1 - 2026-10-12

**See what you have.** Materials in the crafting panel and build menu now show what you have next to
what's needed, for example `312/5`, counting nearby chests as well as your inventory. Large piles are
shortened (`1240` shows as `1.2k`) so they fit. New `ShowAvailableCounts` setting, on by default;
turn it off for the vanilla needed-only number.

## 1.1.0 - 2026-09-23

**Ignore a slot.** Middle-click a slot in your inventory and Stack leaves it alone. A pin in the
slot's corner shows it's marked; middle-click again to clear it. The mark is on the slot, not the
item, and is saved with your character. New `IgnoreSlots` setting, on by default.

**Pull build materials.** With a piece selected on your hammer, Ctrl + left click pulls that piece's
materials from nearby chests into your inventory, for building out of range. Each press adds one
more piece's worth on top of what you carry. New `PullBuildMaterials`, `PullBuildMaterialsKey` and
`PullBuildSets` settings.

## 1.0.9 - 2026-09-16

**Tidy merges the smaller half too.** Tidying a chest with one trophy in it while another chest held
a pile of trophies did nothing: the lone trophy counted as that chest's own category, so it was left
alone. Now, if another chest holds more of the open chest's category, the open chest sends its share
over there, the same "most of the group keeps it" rule that Tidy already used when pulling items in.

## 1.0.8 - 2026-09-13

**Group tweaks.** Feathers and Bone Fragments now live with the hides. Bear Paw (`BjornPaw`) and
Ectoplasm are in the Black Forest group, and Chain has moved from the metals to the swamp group.
Existing groups files keep what they have; delete `NearbyChests.groups.txt` to pick up the new
defaults.

## 1.0.7 - 2026-09-12

**Feed stations from chests.** Coal and ore into smelters and kilns, wood into fires and hearths,
fuel into cooking stations and ovens: if you're not carrying it, it comes out of a nearby chest. One
press still adds one item, the same as vanilla. New `StationsFromChests` setting, on by default.
Food and mead stay manual - a cooking station takes several different dishes and a fermenter one
mead base at a time, so the mod would be choosing for you.

**Longer default ranges.** `CraftingRange` is now 30 m (was 20) and `StackingRange` 15 m (was 10).
Existing configs keep the values you already have.

**Tidy merges a split group.** Two chests holding the same category (Needles in one, Chitin in the
other) used to leave each other alone, so they never came together. Tidy now pulls the group into
whichever chest holds the most of it, and the chest you have open wins a tie.

## 1.0.6 - 2026-09-12

- **Tidy gathers strays**: items of the open chest's category are pulled in from chests where they
  don't belong. New `TidyGathers` setting, on by default.
- **One catch-all chest**: items in no group used to claim an empty chest each. They now share a
  single catch-all chest.
- **Plains and Ocean are one group** (`PlainsOcean`). Delete `NearbyChests.groups.txt` to pick up
  the new defaults.

## 1.0.5 - 2026-09-11

- Crafting and stacking got separate ranges: `CraftingRange` for crafting, upgrading and building,
  `StackingRange` for Stack, Tidy and sorting.
- The tested item groups became the shipped defaults.

## 1.0.4 - 2026-09-11

- Tidy puts a homeless group into an empty chest, which becomes that group's chest.
- Chests can be shared: a chest holding one group takes a second, related group (`ShareChests`).

## 1.0.2 - 2026-09-11

- Added the Tidy button to the chest window, as a small icon left of Place stacks.

## 1.0.1 - 2026-09-11

- Food is detected by whether the game produces it, so berries, mushrooms and honey stack as
  ingredients while cooked dishes, meads and potions stay with you.

## 1.0.0 - 2026-09-11

- Craft, upgrade and build from nearby chests.
- Stack sends your items to every nearby chest that already holds them, finds homes for new items,
  and sorts the chests it touched.
