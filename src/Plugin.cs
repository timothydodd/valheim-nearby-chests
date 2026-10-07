using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace NearbyChests
{
    [BepInPlugin(Guid, ModName, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "NearbyChests";
        public const string ModName = "Nearby Chests";
        public const string Version = "1.2.0";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> OnlyUseMarkedChests;
        internal static ConfigEntry<bool> IncludeCartsAndShips;
        internal static ConfigEntry<float> CraftingRange;
        internal static ConfigEntry<float> StackingRange;

        internal static ConfigEntry<bool> CraftFromChests;
        internal static ConfigEntry<bool> BuildFromChests;
        internal static ConfigEntry<bool> StationsFromChests;

        internal static ConfigEntry<bool> StackToNearby;
        internal static ConfigEntry<KeyboardShortcut> StackAndTidyKey;
        internal static ConfigEntry<bool> KeepHotbar;
        internal static ConfigEntry<bool> ExcludeFood;
        internal static ConfigEntry<bool> ExcludeAmmo;
        internal static ConfigEntry<bool> ExcludeEquipment;
        internal static ConfigEntry<bool> PlaceUnassignedItems;
        internal static ConfigEntry<bool> DontFillEmptyChests;
        internal static ConfigEntry<string> UnassignedItemTypes;
        internal static ConfigEntry<bool> FallbackToOpenChest;
        internal static ConfigEntry<bool> SortAfterStack;
        internal static ConfigEntry<bool> TidyButton;
        internal static ConfigEntry<bool> TidyGathers;
        internal static ConfigEntry<bool> ShareChests;
        internal static ConfigEntry<bool> IgnoreSlots;

        internal static ConfigEntry<bool> PullBuildMaterials;
        internal static ConfigEntry<KeyboardShortcut> PullBuildMaterialsKey;
        internal static ConfigEntry<int> PullBuildSets;

        internal static readonly HashSet<ItemDrop.ItemData.ItemType> UnassignedTypes = new HashSet<ItemDrop.ItemData.ItemType>();

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;

            OnlyUseMarkedChests = Config.Bind("General", "OnlyUseMarkedChests", false,
                "Only use marked chests. Select Include in NearbyChests in a chest window to include it. Disabling this restores legacy eligibility.");

            IncludeCartsAndShips = Config.Bind("General", "IncludeCartsAndShips", false,
                "Also use the storage in nearby carts and ships.");

            CraftingRange = Config.Bind("Crafting", "CraftingRange", 30f,
                new ConfigDescription("How far (in meters) a chest can be and still be used for crafting, upgrading and building.",
                    new AcceptableValueRange<float>(3f, 60f)));
            CraftFromChests = Config.Bind("Crafting", "CraftFromChests", true,
                "Use materials from nearby chests when crafting and upgrading at a station.");
            BuildFromChests = Config.Bind("Crafting", "BuildFromChests", true,
                "Use materials from nearby chests when building with the hammer/hoe/cultivator.");
            StationsFromChests = Config.Bind("Crafting", "StationsFromChests", true,
                "Take fuel and ore from nearby chests when feeding a station you aren't carrying them for: " +
                "coal and ore into smelters and kilns, wood into fires, fuel into cooking stations. " +
                "One press still adds one item, as in vanilla. Food and mead stay manual, because a " +
                "cooking station or fermenter takes several different items and the mod would pick for you.");

            StackingRange = Config.Bind("Stacking", "StackingRange", 15f,
                new ConfigDescription("How far (in meters) a chest can be and still be used by Stack, Tidy and sorting.",
                    new AcceptableValueRange<float>(3f, 60f)));
            StackAndTidyKey = Config.Bind("Stacking", "StackAndTidyKey",
                new KeyboardShortcut(KeyCode.None),
                "Stack your inventory, then tidy each eligible chest within StackingRange once. " +
                "Works without opening a chest. None disables the shortcut. " +
                "Independent of StackToNearby and TidyButton; respects chest and item restrictions.");
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
            DontFillEmptyChests = Config.Bind("Stacking", "DontFillEmptyChests", false,
                "Don't automatically put items into completely empty chests, even when marked. " +
                "Manual deposits are still allowed. Takes effect immediately.");
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
            TidyGathers = Config.Bind("Stacking", "TidyGathers", true,
                "Tidy also pulls items of the open chest's category out of chests where they don't belong " +
                "(a shared chest's second group, strays in another chest, the junk chest). Chests of the same " +
                "category are left alone.");
            TidyButton = Config.Bind("Stacking", "TidyButton", true,
                "Show a Tidy button in the chest window. It moves items that don't match the open chest's category " +
                "to a nearby chest of their own category, an empty chest (which becomes that category's chest), " +
                "or a junk chest (one that's mostly uncategorized items), " +
                "then sorts the chest. Takes effect after a restart.");
            IgnoreSlots = Config.Bind("Stacking", "IgnoreSlots", true,
                "Middle-click a slot in your inventory to mark it ignored (a pin appears in its corner). " +
                "Stack never moves whatever is in an ignored slot. Middle-click again to clear it. " +
                "Marks are saved with your character.");

            PullBuildMaterials = Config.Bind("Building", "PullBuildMaterials", true,
                "With a piece selected on your hammer, press PullBuildMaterialsKey to pull that piece's " +
                "materials from nearby chests (within CraftingRange) into your inventory, for building " +
                "somewhere out of range. Each press adds enough for one more piece on top of what you carry.");
            PullBuildMaterialsKey = Config.Bind("Building", "PullBuildMaterialsKey",
                new KeyboardShortcut(KeyCode.Mouse0, KeyCode.LeftControl),
                "The key (with modifiers) that pulls the selected piece's materials. Default is Ctrl + left click.");
            PullBuildSets = Config.Bind("Building", "PullBuildSets", 1,
                new ConfigDescription("How many pieces' worth of materials each press adds.",
                    new AcceptableValueRange<int>(1, 50)));

            ParseUnassignedTypes();
            ItemGroups.Load();
            UnassignedItemTypes.SettingChanged += (_, __) => ParseUnassignedTypes();
            OnlyUseMarkedChests.SettingChanged += (_, __) => ChestFinder.Invalidate();
            CraftingRange.SettingChanged += (_, __) => ChestFinder.Invalidate();
            StackingRange.SettingChanged += (_, __) => ChestFinder.Invalidate();
            IncludeCartsAndShips.SettingChanged += (_, __) => ChestFinder.Invalidate();

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
            Logger.LogInfo($"{ModName} {Version} loaded");
        }

        private void Update()
        {
            Player player = Player.m_localPlayer;
            KeyboardShortcut shortcut = StackAndTidyKey.Value;
            if (player == null || shortcut.MainKey == KeyCode.None || !player.TakeInput()
                || !ShortcutKeyPressed(shortcut.MainKey, true))
                return;
            // Like BuildPull, allow movement keys alongside the configured modifiers.
            foreach (KeyCode modifier in shortcut.Modifiers)
            {
                if (!ShortcutKeyPressed(modifier, false))
                    return;
            }

            try
            {
                int stacked = Stacker.StackFromPlayer(null, false);
                ChestFinder.Invalidate();
                // Tidy rescans and mutates ChestFinder's shared list; keep our own ordered snapshot.
                var chests = new List<Container>(ChestFinder.GetNearby(player, StackingRange.Value));
                int tidied = 0;
                foreach (Container chest in chests)
                {
                    if (ChestFinder.IsUsable(chest, player, StackingRange.Value)
                        && Tidier.TidyChest(chest, false))
                        tidied++;
                }
                player.Message(MessageHud.MessageType.Center, chests.Count == 0
                    ? "No eligible nearby chests"
                    : $"Stacked {stacked} items; tidied {tidied} {(tidied == 1 ? "chest" : "chests")}");
            }
            finally
            {
                ChestFinder.Invalidate();
            }
        }

        internal static bool ShortcutKeyPressed(KeyCode key, bool down)
        {
            // Valheim rejects keycodes above 349, but Unity assigns F16-F24 values 670-678.
            if (key >= KeyCode.F16 && key <= KeyCode.F24)
            {
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                if (keyboard == null)
                    return false;
                var inputKey = (UnityEngine.InputSystem.Key)((int)UnityEngine.InputSystem.Key.F16
                    + (int)key - (int)KeyCode.F16);
                var control = keyboard[inputKey];
                return down ? control.wasPressedThisFrame : control.isPressed;
            }
            return down ? ZInput.GetKeyDown(key) : ZInput.GetKey(key);
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
