using HarmonyLib;

namespace NearbyChests
{
    /// <summary>
    /// Feeding stations from chests: coal into a smelter, wood into a kiln or fire, food into a
    /// cooking station, and so on.
    ///
    /// Same trick as crafting (see <see cref="Scope"/>). Adding fuel or ore is the game asking the
    /// player's inventory "do you have this?" and then "take one" - the exact calls the crafting
    /// patches already extend to nearby chests. So there's no new station logic here: we just mark
    /// when the game is inside a station interaction, and one press still adds one item, as vanilla.
    ///
    /// Fuel and ore only. Food and mead stay manual: a cooking station or oven takes several different
    /// dishes and a fermenter takes one mead base at a time, so picking for you would grab whatever
    /// the chest happened to hold first. You still put those on yourself.
    ///
    /// Ore is the one exception to "no new logic". The game finds it with <c>Inventory.GetItem(name)</c>
    /// and then spends it with <c>RemoveItem(item, 1)</c>, passing the item instance rather than its
    /// name. When our fallback hands back an item that lives in a chest, that call finds nothing in the
    /// player's inventory to remove, so the patches below remove it from the chest it actually came
    /// from. (<c>RemoveOneItem</c> is patched for the same reason: it's how a cooking station would
    /// spend food, and it ignores the "couldn't remove it" answer, so a chest item would land on the
    /// grill for free. Food never reaches it now, but the hole shouldn't be left open.)
    /// </summary>
    internal static class StationScope
    {
        /// <summary>Open the scope for the local player only; other players' interactions are theirs.</summary>
        internal static void Enter(Humanoid user, ref bool entered)
        {
            entered = Plugin.StationsFromChests.Value && user != null && user == Player.m_localPlayer;
            if (!entered)
                return;
            Scope.Station++;
            Scope.Consume++;
        }

        /// <summary>Same, for a switch on a station: the add-food switch is left manual.</summary>
        internal static void Enter(Switch sw, Humanoid user, ref bool entered)
        {
            if (IsAddFoodSwitch(sw))
            {
                entered = false;
                return;
            }
            Enter(user, ref entered);
        }

        /// <summary>
        /// True for a cooking station's or oven's add-food switch. Its add-fuel switch, and every other
        /// station's switch, is fair game.
        /// </summary>
        private static bool IsAddFoodSwitch(Switch sw)
        {
            if (sw == null)
                return false;
            CookingStation cooking = sw.GetComponentInParent<CookingStation>();
            if (cooking == null)
                cooking = sw.transform.root.GetComponentInChildren<CookingStation>(true);
            return cooking != null && cooking.m_addFoodSwitch == sw;
        }

        internal static void Leave(bool entered)
        {
            if (!entered)
                return;
            Scope.Station--;
            Scope.Consume--;
        }
    }

    // ---- Station entry points --------------------------------------------------------------------

    // Smelters, kilns, windmills, spinning wheels, eitr refineries, shield generators and the cooking
    // station's fuel switch all run through a Switch, so these two cover the lot.
    [HarmonyPatch(typeof(Switch), nameof(Switch.Interact))]
    internal static class Switch_Interact_Patch
    {
        private static void Prefix(Switch __instance, Humanoid character, ref bool __state) =>
            StationScope.Enter(__instance, character, ref __state);

        private static void Finalizer(bool __state) => StationScope.Leave(__state);
    }

    [HarmonyPatch(typeof(Switch), nameof(Switch.UseItem))]
    internal static class Switch_UseItem_Patch
    {
        private static void Prefix(Switch __instance, Humanoid user, ref bool __state) =>
            StationScope.Enter(__instance, user, ref __state);

        private static void Finalizer(bool __state) => StationScope.Leave(__state);
    }

    [HarmonyPatch(typeof(Fireplace), nameof(Fireplace.Interact))]
    internal static class Fireplace_Interact_Patch
    {
        private static void Prefix(Humanoid user, ref bool __state) => StationScope.Enter(user, ref __state);
        private static void Finalizer(bool __state) => StationScope.Leave(__state);
    }

    // ---- Spending an item that turned out to live in a chest --------------------------------------

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem), typeof(ItemDrop.ItemData), typeof(int))]
    internal static class Inventory_RemoveItemInstance_Patch
    {
        private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, int amount, ref bool __result)
        {
            if (!StationItems.TryRemoveFromChest(__instance, item, amount))
                return true;
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem), typeof(ItemDrop.ItemData))]
    internal static class Inventory_RemoveWholeItemInstance_Patch
    {
        private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (!StationItems.TryRemoveFromChest(__instance, item, item != null ? item.m_stack : 0))
                return true;
            __result = true;
            return false;
        }
    }

    // Belt and braces: food doesn't come from chests, but this is the call a cooking station would
    // spend it with, and CookItem ignores the return value - vanilla would refuse to remove a chest
    // item and still put food on the grill for free.
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveOneItem))]
    internal static class Inventory_RemoveOneItem_Patch
    {
        private static bool Prefix(Inventory __instance, ItemDrop.ItemData item, ref bool __result)
        {
            if (!StationItems.TryRemoveFromChest(__instance, item, 1))
                return true;
            __result = true;
            return false;
        }
    }

    internal static class StationItems
    {
        /// <summary>
        /// The game is spending an item from the player's inventory that isn't in it, because our
        /// GetItem fallback handed back one from a chest. Take it out of that chest instead.
        /// Returns false to let vanilla handle the call.
        /// </summary>
        internal static bool TryRemoveFromChest(Inventory inv, ItemDrop.ItemData item, int amount)
        {
            if (item == null || amount <= 0 || Scope.Station <= 0)
                return false;
            if (!Scope.IsLocalPlayerInventory(inv, out Player player) || inv.ContainsItem(item))
                return false;

            foreach (Container c in ChestFinder.GetNearby(player, Plugin.CraftingRange.Value))
            {
                Inventory chest = c.GetInventory();
                if (!chest.ContainsItem(item) || !ChestFinder.EnsureOwner(c))
                    continue;
                chest.RemoveItem(item, amount);
                ChestFinder.InvalidateCounts();
                return true;
            }
            return false;
        }
    }
}
