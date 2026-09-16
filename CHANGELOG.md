# Changelog

Notable changes in each release. The section for a tagged version becomes that release's notes on
GitHub, so keep the headings as `## <version> - <date>`.

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
