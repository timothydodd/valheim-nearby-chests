using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;

namespace NearbyChests
{
    /// <summary>
    /// Sorts items into groups (metals, hides, per-biome materials...) so an item with no home yet can
    /// follow its relatives. Groups live in an editable text file next to the mod's config.
    /// </summary>
    internal static class ItemGroups
    {
        private const string DefaultFile =
@"# Nearby Chests - item groups
#
# When you stack, an item that no nearby chest holds yet goes to the chest holding the most
# items from the same group. If no chest has anything from its group, it goes into an empty chest.
#
# Raw = meat, fish, eggs and other animal ingredients, plus uncooked oven dishes.
# Plants = berries, mushrooms, vegetables, grain, flour, spices and other plant ingredients.
# Anything listed here gets placed this way, whatever its item type. The exception is food, ammo and
# equipment: while the ExcludeFood/ExcludeAmmo/ExcludeEquipment settings are on, those never leave
# your inventory, so the Cooked group only affects how chests are sorted.
#
# One group per line:   GroupName = PrefabName, PrefabName, ...
# - Names are item prefab names (the ones used by the 'spawn' console command). Not case sensitive.
# - A trailing * matches any name starting with that text, e.g. Trophy*
# - If an item is listed in more than one group, the first group wins, so order matters.
# - Items not listed here are only placed if their type is in UnassignedItemTypes in the main
#   config. They're grouped by item type (all trophies together, and so on), except materials,
#   which go into an empty chest.
# - Delete this file to get the latest defaults back.
# Save the file and the change applies the next time you stack; no restart needed.

Metals = CopperOre, Copper, CopperScrap, TinOre, Tin, Bronze, BronzeScrap, BronzeNails, IronOre, IronScrap, Iron, IronNails, SilverOre, Silver, BlackMetalScrap, BlackMetal, FlametalOre, Flametal, FlametalOreNew, FlametalNew, GoldOre, Gold, Coal, Chain, MechanicalSpring, CharredCogwheel
Hides = LeatherScraps, DeerHide, TrollHide, WolfPelt, LoxPelt, ScaleHide, AskHide, MooseHide, BjornHide, SealHide, SerpentScale, BonemawSerpentScale, Leatherstraps
Wood = Wood, FineWood, RoundLog, ElderBark, YggdrasilWood, Blackwood, Frostwood
Stone = Stone, Flint, Obsidian, BlackMarble, Grausten, StoneRock
Seeds = Acorn, BeechSeeds, BirchSeeds, FirCone, FirConeFrost, PineCone, CarrotSeeds, TurnipSeeds, OnionSeeds, KaleSeeds, OatSeeds, PoteitrSeeds
Fishing = FishingBait*, Hook
Raw = RawMeat, DeerMeat, WolfMeat, LoxMeat, SerpentMeat, ChickenMeat, HareMeat, BugMeat, AsksvinMeat, VoltureMeat, BjornMeat, MooseMeat, BoneMawSerpentMeat, SealBlubber, NeckTail, Entrails, FishRaw, FishAnglerRaw, ChickenEgg, AsksvinEgg, VoltureEgg, Bloodbag, CuredSquirrelHamstring, Honey, RoyalJelly, FishAndBreadUncooked, HoneyGlazedChickenUncooked, MeatPlatterUncooked, MisthareSupremeUncooked, BakedPoteitrUncooked, KaleChipsUncooked, LoxPieUncooked, MagicallyStuffedShroomUncooked, OvenPancakeUncooked, PiquantPieUncooked, RoastedCrustPieUncooked, VikingCupcakeUncooked
Plants = Raspberry, Blueberries, Cloudberry, Lingonberry, Vineberry, Pukeberries, Mushroom, MushroomYellow, MushroomBlue, MushroomJotunPuffs, MushroomMagecap, MushroomSmokePuff, Carrot, Turnip, Onion, Kale, Oat, Poteitr, Fiddleheadfern, Dandelion, Barley, BarleyFlour, OatFlour, BreadDough, Thistle, FreshSeaweed, Spice*
Cooked = Cooked*, BakedPoteitr, BlackSoup, BloodPudding, BoarJerky, Bread, CarrotSoup, DeerStew, Eyescream, FierySvinstew, FishAndBread, FishCooked, FishSoup, FishWraps, HoneyGlazedChicken, KaleChips, LoxPie, MagicallyStuffedShroom, MarinatedGreens, MashedMeat, MeatPlatter, MeatballsMashedPoteitr, MinceMeatSauce, MisthareSupreme, MooseKebab, MushroomOmelette, NeckTailGrilled, OatMilk, OatmealLingonberryJam, OnionSoup, OvenPancake, Pancakes, PiquantPie, PulledBear, QueensJam, RoastedCrustPie, Salad, Sausages, ScorchingMedley, SealSoup, SeekerAspic, SerpentMeatCooked, SerpentStew, ShocklateSmoothie, SizzlingBerryBroth, SmokedFish, SmokedMooseMeat, SparklingShroomshake, SpicyMarmalade, TurnipStew, VikingCupcake, WolfJerky, WolfMeatSkewer, YggdrasilPorridge
# Coins are left out on purpose so you keep them for the trader.
Valuables = Amber, AmberPearl, Ruby, SilverNecklace, AncientCoin, AncientGemstone*, CrownJewel
Trophies = Trophy*

BlackForest = BoneFragments, GreydwarfEye, SurtlingCore, AncientSeed, Resin, Feathers, QueenBee, HardAntler
MountainsSwamp = Guck, Ooze, Root, WitheredBone, Crystal, FreezeGland, WolfFang, WolfClaw, DragonTear, PowderedDragonEgg
Plains = Flax, LinenThread, Needle, Tar
Ocean = Chitin
Mistlands = Sap, Softtissue, Carapace, Mandible, Eitr, Bilebag, GiantBloodSack, Wisp, DvergrNeedle, BlackCore
Ashlands = AskBladder, CharcoalResin, CharredBone, Charredskull, CelestialFeather, MoltenCore, MorgenHeart, MorgenSinew, ProustitePowder, SulfurStone, BonemawSerpentTooth, GemstoneRed, GemstoneGreen, GemstoneBlue, AsksvinCarrionNeck, AsksvinCarrionPelvic, AsksvinCarrionRibcage, AsksvinCarrionSkull
DeepNorth = BjornPaw, MooseSinew, Ice, FrozenFuel, OozeMork, ElakingHairBundle, UndeadBjornRibcage, BarkaBranch
";

        private static readonly Dictionary<string, string> Exact = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<KeyValuePair<string, string>> Prefixes = new List<KeyValuePair<string, string>>();
        private static readonly Dictionary<string, string> Resolved = new Dictionary<string, string>();
        private static readonly List<string> Order = new List<string>();
        private static DateTime _loadedWriteTime;
        private const string TypePrefix = "type:";

        public static string FilePath => Path.Combine(Paths.ConfigPath, Plugin.Guid + ".groups.txt");

        /// <summary>Load the groups file (writing the default one first if it doesn't exist).</summary>
        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    File.WriteAllText(FilePath, DefaultFile);
                _loadedWriteTime = File.GetLastWriteTimeUtc(FilePath);
                Parse(File.ReadAllLines(FilePath));
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Couldn't read {FilePath}, using built-in groups: {e.Message}");
                Parse(DefaultFile.Split('\n'));
            }
        }

        /// <summary>Pick up edits made while the game is running.</summary>
        public static void ReloadIfChanged()
        {
            try
            {
                if (File.Exists(FilePath) && File.GetLastWriteTimeUtc(FilePath) != _loadedWriteTime)
                    Load();
            }
            catch (IOException)
            {
                // File is mid-save in an editor; try again next stack.
            }
        }

        private static void Parse(IEnumerable<string> lines)
        {
            Exact.Clear();
            Prefixes.Clear();
            Resolved.Clear();
            Order.Clear();
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;
                int eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;

                string group = line.Substring(0, eq).Trim();
                if (!Order.Contains(group))
                    Order.Add(group);
                foreach (string part in line.Substring(eq + 1).Split(','))
                {
                    string name = part.Trim();
                    if (name.Length == 0)
                        continue;
                    if (name.EndsWith("*"))
                        Prefixes.Add(new KeyValuePair<string, string>(name.TrimEnd('*'), group));
                    else if (!Exact.ContainsKey(name))
                        Exact[name] = group;
                }
            }
        }

        /// <summary>Position of the item's group in the file, so sorted chests keep groups together.</summary>
        public static int Rank(ItemDrop.ItemData item) => RankOf(Of(item));

        /// <summary>Position of a group in the file; unlisted groups sort last.</summary>
        public static int RankOf(string group)
        {
            int index = Order.IndexOf(group ?? "");
            return index < 0 ? int.MaxValue : index;
        }

        /// <summary>True if the groups file lists this item, as opposed to grouping it by type.</summary>
        public static bool IsListed(ItemDrop.ItemData item)
        {
            string group = Of(item);
            return group != null && !group.StartsWith(TypePrefix);
        }

        /// <summary>
        /// The group an item belongs to, or null if it should only ever go into an empty chest.
        /// </summary>
        public static string Of(ItemDrop.ItemData item)
        {
            string prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : item.m_shared.m_name;
            if (Resolved.TryGetValue(prefab, out string cached))
                return cached;

            string group = null;
            if (!Exact.TryGetValue(prefab, out group))
            {
                foreach (KeyValuePair<string, string> p in Prefixes)
                {
                    if (prefab.StartsWith(p.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        group = p.Value;
                        break;
                    }
                }
            }

            // Unlisted non-materials still cluster by type (food with food, ammo with ammo).
            // Unlisted materials don't: nearly every chest has some material in it.
            // Weapons, armor and tools count as one group, so a mixed gear chest stays together.
            ItemDrop.ItemData.ItemType type = item.m_shared.m_itemType;
            if (group == null && IsEquipment(type))
                group = TypePrefix + "Equipment";
            else if (group == null && IsAmmo(type))
                group = TypePrefix + "Ammo";
            else if (group == null && type != ItemDrop.ItemData.ItemType.Material)
                group = TypePrefix + type;

            Resolved[prefab] = group;
            return group;
        }

        public static bool IsAmmo(ItemDrop.ItemData.ItemType type) =>
            type == ItemDrop.ItemData.ItemType.Ammo || type == ItemDrop.ItemData.ItemType.AmmoNonEquipable;

        public static bool IsEquipment(ItemDrop.ItemData.ItemType type)
        {
            switch (type)
            {
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                case ItemDrop.ItemData.ItemType.Bow:
                case ItemDrop.ItemData.ItemType.Attach_Atgeir:
                case ItemDrop.ItemData.ItemType.Shield:
                case ItemDrop.ItemData.ItemType.Helmet:
                case ItemDrop.ItemData.ItemType.Chest:
                case ItemDrop.ItemData.ItemType.Legs:
                case ItemDrop.ItemData.ItemType.Hands:
                case ItemDrop.ItemData.ItemType.Shoulder:
                case ItemDrop.ItemData.ItemType.Utility:
                case ItemDrop.ItemData.ItemType.Trinket:
                case ItemDrop.ItemData.ItemType.Tool:
                case ItemDrop.ItemData.ItemType.Torch:
                case ItemDrop.ItemData.ItemType.Customization:
                    return true;
                default:
                    return false;
            }
        }
    }
}
