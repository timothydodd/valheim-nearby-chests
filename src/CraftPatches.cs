using HarmonyLib;

namespace NearbyChests
{
    /// <summary>
    /// Crafting/building from chests.
    ///
    /// Rather than rewrite Valheim's requirement checks, we mark when the game is inside one of them
    /// ("count scope") or is spending materials ("consume scope"). While a scope is active, calls on the
    /// local player's inventory are extended to include nearby chests. Everywhere else the inventory
    /// behaves exactly as vanilla.
    /// </summary>
    internal static class Scope
    {
        internal static int Craft;
        internal static int Build;
        internal static int Consume;

        internal static bool Counting =>
            (Craft > 0 && Plugin.CraftFromChests.Value) || (Build > 0 && Plugin.BuildFromChests.Value);

        internal static bool IsLocalPlayerInventory(Inventory inv, out Player player)
        {
            player = Player.m_localPlayer;
            return player != null && inv == player.GetInventory();
        }
    }

    // ---- Scope markers -------------------------------------------------------------------------

    [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirementItems))]
    internal static class Player_HaveRequirementItems_Patch
    {
        private static void Prefix() => Scope.Craft++;
        private static void Finalizer() => Scope.Craft--;
    }

    [HarmonyPatch(typeof(Player), nameof(Player.GetFirstRequiredItem))]
    internal static class Player_GetFirstRequiredItem_Patch
    {
        private static void Prefix() => Scope.Craft++;
        private static void Finalizer() => Scope.Craft--;
    }

    [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), typeof(Piece), typeof(Player.RequirementMode))]
    internal static class Player_HaveRequirementsPiece_Patch
    {
        private static void Prefix() => Scope.Build++;
        private static void Finalizer() => Scope.Build--;
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.SetupRequirement))]
    internal static class InventoryGui_SetupRequirement_Patch
    {
        private static void Prefix(bool craft)
        {
            if (craft) Scope.Craft++; else Scope.Build++;
        }

        private static void Finalizer(bool craft)
        {
            if (craft) Scope.Craft--; else Scope.Build--;
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.ConsumeResources))]
    internal static class Player_ConsumeResources_Patch
    {
        private static void Prefix() => Scope.Consume++;
        private static void Finalizer() => Scope.Consume--;
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
    internal static class InventoryGui_DoCrafting_Patch
    {
        private static void Prefix() => Scope.Consume++;
        private static void Finalizer() => Scope.Consume--;
    }

    // ---- Inventory extensions (only active inside a scope) --------------------------------------

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.CountItems))]
    internal static class Inventory_CountItems_Patch
    {
        private static void Postfix(Inventory __instance, string name, int quality, bool matchWorldLevel, ref int __result)
        {
            if (name == null || !Scope.Counting || !Scope.IsLocalPlayerInventory(__instance, out Player player))
                return;
            __result += ChestFinder.Count(player, name, quality, matchWorldLevel);
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.HaveItem), typeof(string), typeof(bool))]
    internal static class Inventory_HaveItem_Patch
    {
        private static void Postfix(Inventory __instance, string name, bool matchWorldLevel, ref bool __result)
        {
            if (__result || !Scope.Counting || !Scope.IsLocalPlayerInventory(__instance, out Player player))
                return;
            __result = ChestFinder.Have(player, name, matchWorldLevel);
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetItem), typeof(string), typeof(int), typeof(bool))]
    internal static class Inventory_GetItem_Patch
    {
        private static void Postfix(Inventory __instance, string name, int quality, bool isPrefabName, ref ItemDrop.ItemData __result)
        {
            if (__result != null || isPrefabName || !Scope.Counting || !Scope.IsLocalPlayerInventory(__instance, out Player player))
                return;
            __result = ChestFinder.Find(player, name, quality);
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem), typeof(string), typeof(int), typeof(int), typeof(bool))]
    internal static class Inventory_RemoveItemByName_Patch
    {
        // Spend what the player is carrying first; take only the shortfall from chests.
        private static void Prefix(Inventory __instance, string name, int amount, int itemQuality, bool worldLevelBased)
        {
            if (Scope.Consume <= 0 || !Scope.IsLocalPlayerInventory(__instance, out Player player))
                return;

            int carried = 0;
            foreach (ItemDrop.ItemData item in __instance.GetAllItems())
            {
                if (item.m_shared.m_name == name
                    && (itemQuality < 0 || item.m_quality == itemQuality)
                    && (!worldLevelBased || item.m_worldLevel >= Game.m_worldLevel))
                {
                    carried += item.m_stack;
                }
            }

            if (carried < amount)
                ChestFinder.Remove(player, name, amount - carried, itemQuality, worldLevelBased);
        }
    }

    // ---- Container bookkeeping ------------------------------------------------------------------

    [HarmonyPatch(typeof(Container), nameof(Container.Awake))]
    internal static class Container_Awake_Patch
    {
        private static void Postfix(Container __instance)
        {
            if (__instance.GetInventory() != null)
                ChestFinder.Register(__instance);
        }
    }

    [HarmonyPatch(typeof(Container), nameof(Container.OnContainerChanged))]
    internal static class Container_OnContainerChanged_Patch
    {
        private static void Postfix() => ChestFinder.InvalidateCounts();
    }
}
