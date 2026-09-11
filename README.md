# Nearby Chests

[![Build](https://github.com/timothydodd/valheim-nearby-chests/actions/workflows/build.yml/badge.svg)](https://github.com/timothydodd/valheim-nearby-chests/actions/workflows/build.yml)

A client-side [BepInEx](https://github.com/BepInEx/BepInEx) mod for Valheim. It lets you craft and
build straight from nearby chests, and makes the Stack button put your items away into the right
chests for you.

## Features

1. **Craft and build from nearby chests.** Workbench/forge recipes, upgrades, and hammer builds
   can use materials sitting in chests within range (20 m by default). The requirement counts in the
   crafting panel and build menu include those chests. Items you carry are spent first. After that the
   rest comes from the closest chests.
2. **Stack to every nearby chest.** With a chest open, pressing **Stack** (or holding **E** on a
   chest) sends each stackable item in your inventory to every nearby chest that already holds
   that item. The chest you're using gets first pick.
3. **New items find a home.** If no nearby chest holds an item yet (or its chests are full), the mod
   looks for somewhere similar:
   1. **A chest with similar items.** It picks the nearby chest holding the most items from the same
      group: metals, hides, wood, stone, raw ingredients, plants, cooked food, seeds, trophies, or
      materials from the same biome (Meadows, Black Forest, Swamp, and so on).
   2. **An empty chest.** If no chest has anything from that group, the item goes into an empty
      chest. The one you're using comes first if it's empty, then the nearest. That chest becomes
      the group's home next time.
   3. **Your inventory.** If there's no empty chest either, the item stays with you, and the
      on-screen message tells you how many were left over.
4. **Tidy chests.** Every chest that receives items gets its partial stacks merged and its
   contents sorted by type, then group, then name.

Stacking leaves these in your inventory. Each has its own setting, and all are on by default:
- **Food, meads and potions.** Edible items that are also used in a recipe (berries, mushrooms,
  honey and so on) count as ingredients and still get stacked.
- **Ammo:** arrows, bolts, bait.
- **Equipment:** weapons, armor, shields, tools, torches, utility items, trinkets.
- **Your hotbar** (the top row), and anything you have equipped.

Chests the mod never touches:
- The Obliterator.
- Gravestones.
- Dungeon loot chests.
- Chests another player has open.
- Chests you can't open yourself (private, or behind someone else's ward).

Carts and ships are off by default.

## Install

Download the latest zip from [Releases](https://github.com/timothydodd/valheim-nearby-chests/releases):

| File | Use it when |
|---|---|
| `NearbyChests-x.y.z-with-BepInEx.zip` | You don't have BepInEx yet. Includes BepInExPack_Valheim. |
| `NearbyChests-x.y.z.zip` | You already have BepInEx installed. |

1. Extract the zip into your Valheim folder, so that `BepInEx\` ends up next to `valheim.exe`.
   On Steam, that's *right-click Valheim → Manage → Browse local files*.
2. Launch the game normally.
3. To check it loaded, open `BepInEx\LogOutput.log` and look for `Nearby Chests x.y.z loaded`.

To uninstall, delete `BepInEx\plugins\NearbyChests`. To remove BepInEx entirely, also delete
`winhttp.dll` from the Valheim folder.

## Configuration

After the first launch, settings are in `BepInEx\config\NearbyChests.cfg`:

| Section  | Setting              | Default          | What it does |
|----------|----------------------|------------------|--------------|
| General  | Range                | 20               | Distance in meters that counts as "nearby" (3–60). |
| General  | IncludeCartsAndShips | false            | Also use cart and ship storage. |
| Crafting | CraftFromChests      | true             | Use chest materials at crafting stations. |
| Crafting | BuildFromChests      | true             | Use chest materials when building. |
| Stacking | StackToNearby        | true             | Stack button pushes to all nearby chests. Turn off for the vanilla button. |
| Stacking | KeepHotbar           | true             | Never stack items from the top row. |
| Stacking | ExcludeFood          | true             | Never stack food, meads or potions. Recipe ingredients still stack. |
| Stacking | ExcludeAmmo          | true             | Never stack arrows, bolts, bait or other ammo. |
| Stacking | ExcludeEquipment     | true             | Never stack weapons, armor, shields, tools, torches, utility items or trinkets. |
| Stacking | PlaceUnassignedItems | true             | Items with no home go to a chest of similar items, or an empty chest. |
| Stacking | UnassignedItemTypes  | Material,Trophy  | Item types placed even when not in the groups file. Other options: Consumable, Ammo, AmmoNonEquipable, Fish, Misc. |
| Stacking | FallbackToOpenChest  | false            | If there's no similar or empty chest, use the open chest instead of leaving items in your inventory. |
| Stacking | SortAfterStack       | true             | Sort and merge chests that received items. |

### Item groups

Groups live in `BepInEx\config\NearbyChests.groups.txt`, one line per group:

```
Metals = CopperOre, Copper, CopperScrap, TinOre, Tin, Bronze, ...
Raw = RawMeat, DeerMeat, ..., ChickenEgg, Honey, ...
Plants = Raspberry, Blueberries, Mushroom, Carrot, ..., BarleyFlour, Spice*
Cooked = Cooked*, BakedPoteitr, BoarJerky, Bread, ...
Trophies = Trophy*
```

- **Names:** item prefab names (the ones the `spawn` console command uses). Not case sensitive.
- **Wildcards:** a trailing `*` matches any name starting with that text.
- **Order:** if an item is listed twice, the first group wins.
- **Always placed:** anything listed is placed even if its type isn't in `UnassignedItemTypes`.
  Meads and coins aren't listed, so they only move when a chest already holds them. Food, ammo and
  equipment are never stacked while their `Exclude...` settings are on, whatever group they're in.
- **Live edits:** changes apply the next time you stack, with no restart needed.
- **Resetting:** delete the file to get the latest defaults back.

## Multiplayer

The mod is client-side, so only you need it and the server doesn't. Before changing a chest, it takes
network ownership of it, the same way the game's own "Take All" does, and it skips any chest another
player has open. There is a small timing window: if another player changes a chest in the same
moment you craft from it or stack into it, one of the changes can be lost.

## Building

Requirements:
- **.NET SDK 6 or newer.**
- **A Valheim install to compile against.** The game client or the free dedicated server both work.

The game's DLLs aren't in this repo, so you have to tell the build where Valheim is. Do one of
these:

- Pass it on the command line:
  ```
  dotnet build -c Release -p:ValheimDir="D:\Steam\steamapps\common\Valheim"
  ```
- Create a `ValheimDir.props` file next to `NearbyChests.csproj`. It's git-ignored:
  ```xml
  <Project>
    <PropertyGroup Condition="'$(ValheimDir)' == ''">
      <ValheimDir>D:\Steam\steamapps\common\Valheim</ValheimDir>
    </PropertyGroup>
  </Project>
  ```
- Install Valheim at the default Steam location,
  `C:\Program Files (x86)\Steam\steamapps\common\Valheim`, which the build uses automatically.

The DLL lands in `bin/Release/NearbyChests.dll`. To make the release zips:

```
python3 build/package.py 1.0.0 path/to/BepInExPack_Valheim.zip
```

`lib/` holds `BepInEx.dll` (LGPL-2.1) and `0Harmony.dll` (MIT) from BepInExPack_Valheim so the
project builds without BepInEx installed. After a Valheim update, rebuild. If the game changed
something the mod relies on, you get a build error rather than a mod that silently misbehaves.

### CI and releases

On every push and pull request, [the build workflow](.github/workflows/build.yml):
1. downloads the Valheim dedicated server with SteamCMD,
2. builds against its DLLs,
3. uploads both zips as a workflow artifact.

To publish a release:
1. Bump `Version` in `src/Plugin.cs`.
2. Commit and push.
3. Tag and push the tag:
   ```
   git tag v1.0.1 && git push origin v1.0.1
   ```

The workflow builds the tag and attaches the zips to a GitHub release. It fails if the tag doesn't
match `Plugin.Version`.

## License

[MIT](LICENSE)
