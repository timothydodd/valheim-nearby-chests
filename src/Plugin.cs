using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace NearbyChests
{
    [BepInPlugin(Guid, ModName, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "NearbyChests";
        public const string ModName = "Nearby Chests";
        public const string Version = "1.0.5";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> IncludeCartsAndShips;
        internal static ConfigEntry<float> CraftingRange;
        internal static ConfigEntry<float> StackingRange;

        internal static ConfigEntry<bool> CraftFromChests;
        internal static ConfigEntry<bool> BuildFromChests;

        internal static ConfigEntry<bool> StackToNearby;
        internal static ConfigEntry<bool> KeepHotbar;
        internal static ConfigEntry<bool> ExcludeFood;
        internal static ConfigEntry<bool> ExcludeAmmo;
        internal static ConfigEntry<bool> ExcludeEquipment;
        internal static ConfigEntry<bool> PlaceUnassignedItems;
        internal static ConfigEntry<string> UnassignedItemTypes;
        internal static ConfigEntry<bool> FallbackToOpenChest;
        internal static ConfigEntry<bool> SortAfterStack;
        internal static ConfigEntry<bool> TidyButton;
        internal static ConfigEntry<bool> ShareChests;

        internal static readonly HashSet<ItemDrop.ItemData.ItemType> UnassignedTypes = new HashSet<ItemDrop.ItemData.ItemType>();

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            IncludeCartsAndShips = Config.Bind("General", "IncludeCartsAndShips", false,
                "Also use the storage in nearby carts and ships.");

            CraftingRange = Config.Bind("Crafting", "CraftingRange", 20f,
                new ConfigDescription("How far (in meters) a chest can be and still be used for crafting, upgrading and building.",
                    new AcceptableValueRange<float>(3f, 60f)));
            CraftFromChests = Config.Bind("Crafting", "CraftFromChests", true,
                "Use materials from nearby chests when crafting and upgrading at a station.");
            BuildFromChests = Config.Bind("Crafting", "BuildFromChests", true,
                "Use materials from nearby chests when building with the hammer/hoe/cultivator.");

            StackingRange = Config.Bind("Stacking", "StackingRange", 10f,
                new ConfigDescription("How far (in meters) a chest can be and still be used by Stack, Tidy and sorting.",
                    new AcceptableValueRange<float>(3f, 60f)));
            StackToNearby = Config.Bind("Stacking", "StackToNearby", true,
                "When you press the Stack button on an open chest, send your items to every nearby chest that already holds that item.");
            KeepHotbar = Config.Bind("Stacking", "KeepHotbar", true,
                "Never stack items from your hotbar (the top row of your inventory).");
            ExcludeFood = Config.Bind("Stacking", "ExcludeFood", true,
                "Never stack food, meads or potions you're carrying - anything cooked, baked, crafted or brewed. " +
                "Edible things you pick or harvest (berries, mushrooms, honey...) count as ingredients and still get stacked.");
            ExcludeAmmo = Config.Bind("Stacking", "ExcludeAmmo", true,
                "Never stack arrows, bolts, bait or other ammo you're carrying.");
            ExcludeEquipment = Config.Bind("Stacking", "ExcludeEquipment", true,
                "Never stack weapons, armor, shields, tools, torches, utility items or trinkets.");
            PlaceUnassignedItems = Config.Bind("Stacking", "PlaceUnassignedItems", true,
                "Items that no nearby chest holds yet go to the chest with the most similar items " +
                "(metals with metals, hides with hides, same biome...), or into an empty chest if none match. " +
                "Groups are defined in " + Guid + ".groups.txt. Items listed there are always placed; " +
                "other items only if their type is in UnassignedItemTypes.");
            UnassignedItemTypes = Config.Bind("Stacking", "UnassignedItemTypes", "Material,Trophy",
                "Comma-separated item types that PlaceUnassignedItems may move when the item isn't listed in the groups file. " +
                "Options: Material, Trophy, Consumable, Ammo, AmmoNonEquipable, Fish, Misc.");
            FallbackToOpenChest = Config.Bind("Stacking", "FallbackToOpenChest", false,
                "If an unassigned item has no similar chest and there is no empty chest, put it in the chest you have open. " +
                "When off, it stays in your inventory.");
            SortAfterStack = Config.Bind("Stacking", "SortAfterStack", true,
                "Sort and merge the contents of every chest that received items.");
            ShareChests = Config.Bind("Stacking", "ShareChests", true,
                "When a group has no chest of its own and there's no empty chest, let a chest that holds only one " +
                "group take a second one, preferring related groups (neighbours in the groups file).");
            TidyButton = Config.Bind("Stacking", "TidyButton", true,
                "Show a Tidy button in the chest window. It moves items that don't match the open chest's category " +
                "to a nearby chest of their own category, an empty chest (which becomes that category's chest), " +
                "or a junk chest (one that's mostly uncategorized items), " +
                "then sorts the chest. Takes effect after a restart.");

            ParseUnassignedTypes();
            ItemGroups.Load();
            UnassignedItemTypes.SettingChanged += (_, __) => ParseUnassignedTypes();
            CraftingRange.SettingChanged += (_, __) => ChestFinder.Invalidate();
            StackingRange.SettingChanged += (_, __) => ChestFinder.Invalidate();
            IncludeCartsAndShips.SettingChanged += (_, __) => ChestFinder.Invalidate();

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo($"{ModName} {Version} loaded");
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }

        private static void ParseUnassignedTypes()
        {
            UnassignedTypes.Clear();
            foreach (string part in UnassignedItemTypes.Value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    UnassignedTypes.Add((ItemDrop.ItemData.ItemType)Enum.Parse(typeof(ItemDrop.ItemData.ItemType), part.Trim(), true));
                }
                catch (ArgumentException)
                {
                    // Ignore unknown type names rather than break the mod over a typo.
                }
            }
        }
    }
}
