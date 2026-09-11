using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace NearbyChests
{
    /// <summary>
    /// Replaces the chest "Stack" action: items go to every nearby chest that already holds them,
    /// items without a home go to a chest of similar items (or an empty one), and chests that
    /// received items get sorted.
    /// </summary>
    internal static class Stacker
    {
        public static void StackFromPlayer(Container opened)
        {
            Player player = Player.m_localPlayer;
            if (player == null || opened == null)
                return;

            Inventory playerInv = player.GetInventory();

            // The open chest gets first pick, then the rest nearest-first.
            ChestFinder.Invalidate();
            var chests = new List<Container> { opened };
            foreach (Container c in ChestFinder.GetNearby(player))
            {
                if (c != opened)
                    chests.Add(c);
            }

            ItemGroups.ReloadIfChanged();
            var touched = new HashSet<Container>();
            int moved = 0;
            int homeless = 0;

            foreach (ItemDrop.ItemData item in playerInv.GetAllItems().ToList())
            {
                if (!CanStack(player, item))
                    continue;

                // 1. Established homes: chests that already contain this item.
                foreach (Container c in chests)
                {
                    if (!c.GetInventory().ContainsItemByName(item.m_shared.m_name))
                        continue;
                    moved += MoveInto(c, item, playerInv, touched);
                    if (!playerInv.ContainsItem(item))
                        break;
                }

                // 2. No home (or homes full): find it one.
                if (playerInv.ContainsItem(item)
                    && Plugin.PlaceUnassignedItems.Value
                    && (ItemGroups.IsListed(item) || Plugin.UnassignedTypes.Contains(item.m_shared.m_itemType)))
                {
                    moved += PlaceUnassigned(item, playerInv, chests, opened, touched);
                    if (playerInv.ContainsItem(item))
                        homeless += item.m_stack;
                }
            }

            playerInv.Changed();

            if (Plugin.SortAfterStack.Value)
            {
                foreach (Container c in touched)
                    Sort(c.GetInventory());
            }

            foreach (Container c in touched)
                InventoryGui.instance.m_moveItemEffects.Create(c.transform.position, Quaternion.identity);

            string note = homeless > 0 ? $"\n{homeless} items had no similar or empty chest" : "";
            if (moved > 0)
            {
                string where = touched.Count == 1 ? "1 chest" : touched.Count + " chests";
                player.Message(MessageHud.MessageType.Center, $"Stacked {moved} items into {where}{note}");
                Game.instance.IncrementPlayerStat(PlayerStatType.PlaceStacks);
            }
            else
            {
                player.Message(MessageHud.MessageType.Center, note.Length > 0 ? note.TrimStart('\n') : "$msg_stackall_none");
            }
        }

        /// <summary>
        /// Give an item with no home a place to live:
        /// 1. the chest holding the most items from the same group (metals, hides, same biome...),
        /// 2. otherwise an empty chest (the open one if it's empty, then the nearest),
        /// 3. otherwise, if enabled, the open chest.
        /// </summary>
        private static int PlaceUnassigned(ItemDrop.ItemData item, Inventory from, List<Container> chests,
            Container opened, HashSet<Container> touched)
        {
            int moved = 0;

            string group = ItemGroups.Of(item);
            if (group != null)
            {
                // chests is already open-chest-first then nearest, and OrderByDescending is stable,
                // so ties go to the closer chest.
                var similar = chests
                    .Select(c => new { Chest = c, Score = GroupScore(c.GetInventory(), group) })
                    .Where(x => x.Score > 0)
                    .OrderByDescending(x => x.Score)
                    .Select(x => x.Chest)
                    .ToList();
                foreach (Container c in similar)
                {
                    moved += MoveInto(c, item, from, touched);
                    if (!from.ContainsItem(item))
                        return moved;
                }
            }

            foreach (Container c in chests)
            {
                if (c.GetInventory().NrOfItems() != 0)
                    continue;
                moved += MoveInto(c, item, from, touched);
                if (!from.ContainsItem(item))
                    return moved;
            }

            if (Plugin.FallbackToOpenChest.Value)
                moved += MoveInto(opened, item, from, touched);
            return moved;
        }

        /// <summary>How many stacks in the inventory belong to the group.</summary>
        internal static int GroupScore(Inventory inv, string group)
        {
            int score = 0;
            foreach (ItemDrop.ItemData i in inv.GetAllItems())
            {
                if (ItemGroups.Of(i) == group)
                    score++;
            }
            return score;
        }

        private static bool CanStack(Player player, ItemDrop.ItemData item)
        {
            if (item.m_shared.m_maxStackSize <= 1 || item.m_shared.m_questItem)
                return false;
            if (item.m_equipped || player.IsItemEquiped(item))
                return false;
            if (Plugin.KeepHotbar.Value && item.m_gridPos.y == 0)
                return false;
            if (Plugin.ExcludeFood.Value && IsFood(item))
                return false;
            if (Plugin.ExcludeAmmo.Value && ItemGroups.IsAmmo(item.m_shared.m_itemType))
                return false;
            if (Plugin.ExcludeEquipment.Value && ItemGroups.IsEquipment(item.m_shared.m_itemType))
                return false;
            return true;
        }

        private static HashSet<string> _madeItems;
        private static int _madeFromRecipes = -1;

        /// <summary>
        /// Food, meads and potions: anything consumable that something in the game makes - a crafting
        /// recipe, a cooking station or oven, or a fermenter. Berries, mushrooms or honey are edible but
        /// are picked or harvested rather than made, so they count as ingredients and still stack.
        /// (Checking "used in a recipe" instead doesn't work: stews and sausages are feast ingredients.)
        /// </summary>
        private static bool IsFood(ItemDrop.ItemData item)
        {
            if (item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Consumable)
                return false;
            HashSet<string> made = MadeItems();
            return made == null || made.Contains(item.m_shared.m_name);
        }

        /// <summary>Names of every item the game can produce. Null until the world has loaded.</summary>
        private static HashSet<string> MadeItems()
        {
            ObjectDB db = ObjectDB.instance;
            ZNetScene scene = ZNetScene.instance;
            if (db == null || scene == null)
                return null;
            if (_madeItems != null && _madeFromRecipes == db.m_recipes.Count)
                return _madeItems;

            var made = new HashSet<string>();
            foreach (Recipe recipe in db.m_recipes)
                AddMade(made, recipe != null ? recipe.m_item : null);

            foreach (GameObject prefab in scene.m_prefabs)
            {
                if (prefab == null)
                    continue;
                CookingStation cooking = prefab.GetComponent<CookingStation>();
                if (cooking != null)
                    foreach (CookingStation.ItemConversion c in cooking.m_conversion)
                        AddMade(made, c.m_to);
                Fermenter fermenter = prefab.GetComponent<Fermenter>();
                if (fermenter != null)
                    foreach (Fermenter.ItemConversion c in fermenter.m_conversion)
                        AddMade(made, c.m_to);
                Smelter smelter = prefab.GetComponent<Smelter>();
                if (smelter != null)
                    foreach (Smelter.ItemConversion c in smelter.m_conversion)
                        AddMade(made, c.m_to);
            }

            _madeItems = made;
            _madeFromRecipes = db.m_recipes.Count;
            return made;
        }

        private static void AddMade(HashSet<string> made, ItemDrop item)
        {
            if (item != null)
                made.Add(item.m_itemData.m_shared.m_name);
        }

        /// <summary>Move as much of <paramref name="item"/> as fits into the chest. Returns the count moved.</summary>
        internal static int MoveInto(Container chest, ItemDrop.ItemData item, Inventory from, HashSet<Container> touched)
        {
            Inventory to = chest.GetInventory();
            if (!to.HaveEmptySlot() && to.FindFreeStackSpace(item.m_shared.m_name, item.m_worldLevel) <= 0)
                return 0;
            if (!ChestFinder.EnsureOwner(chest))
                return 0;

            int before = item.m_stack;
            int moved;
            // AddItem returns true when the whole stack found room; otherwise it shrinks item.m_stack
            // by however much it managed to place.
            if (to.AddItem(item))
            {
                from.RemoveItem(item);
                moved = before;
            }
            else
            {
                moved = before - item.m_stack;
            }

            if (moved > 0)
                touched.Add(chest);
            return moved;
        }

        /// <summary>Merge partial stacks and lay items out by type, then group, then name.</summary>
        public static void Sort(Inventory inv)
        {
            List<ItemDrop.ItemData> items = inv.GetAllItems();
            List<ItemDrop.ItemData> ordered = items
                .OrderBy(i => TypeRank(i.m_shared.m_itemType))
                .ThenBy(i => ItemGroups.Rank(i))
                .ThenBy(i => Localization.instance.Localize(i.m_shared.m_name))
                .ThenByDescending(i => i.m_quality)
                .ThenByDescending(i => i.m_stack)
                .ToList();

            var result = new List<ItemDrop.ItemData>();
            foreach (ItemDrop.ItemData item in ordered)
            {
                if (item.m_shared.m_maxStackSize > 1)
                {
                    foreach (ItemDrop.ItemData existing in result)
                    {
                        if (item.m_stack <= 0)
                            break;
                        if (!existing.IsSameType(item) || existing.m_quality != item.m_quality
                            || existing.m_cheated != item.m_cheated || existing.m_stack >= existing.m_shared.m_maxStackSize)
                            continue;
                        int amount = Mathf.Min(existing.m_shared.m_maxStackSize - existing.m_stack, item.m_stack);
                        existing.m_stack += amount;
                        item.m_stack -= amount;
                    }
                }
                if (item.m_stack > 0)
                    result.Add(item);
            }

            int width = inv.GetWidth();
            for (int i = 0; i < result.Count; i++)
                result[i].m_gridPos = new Vector2i(i % width, i / width);

            items.Clear();
            items.AddRange(result);
            inv.Changed();
        }

        private static int TypeRank(ItemDrop.ItemData.ItemType type)
        {
            switch (type)
            {
                case ItemDrop.ItemData.ItemType.Material: return 0;
                case ItemDrop.ItemData.ItemType.Trophy: return 1;
                case ItemDrop.ItemData.ItemType.Fish: return 2;
                case ItemDrop.ItemData.ItemType.Consumable: return 3;
                case ItemDrop.ItemData.ItemType.Ammo:
                case ItemDrop.ItemData.ItemType.AmmoNonEquipable: return 4;
                case ItemDrop.ItemData.ItemType.Misc: return 5;
                default: return 10 + (int)type;
            }
        }
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnStackAll))]
    internal static class InventoryGui_OnStackAll_Patch
    {
        private static bool Prefix(InventoryGui __instance)
        {
            if (!Plugin.StackToNearby.Value || Player.m_localPlayer == null || Player.m_localPlayer.IsTeleporting()
                || __instance.m_currentContainer == null)
                return true;

            __instance.SetupDragItem(null, null, 1);
            Stacker.StackFromPlayer(__instance.m_currentContainer);
            return false;
        }
    }

    // Holding Use on an open chest goes through this RPC instead of the button.
    [HarmonyPatch(typeof(Container), nameof(Container.RPC_StackResponse))]
    internal static class Container_RPC_StackResponse_Patch
    {
        private static bool Prefix(Container __instance, bool granted)
        {
            if (!Plugin.StackToNearby.Value || !granted || Player.m_localPlayer == null)
                return true;

            Stacker.StackFromPlayer(__instance);
            return false;
        }
    }
}
