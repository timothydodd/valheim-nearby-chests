using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NearbyChests
{
    /// <summary>
    /// The Tidy button cleans out the chest you have open.
    ///
    /// Each chest's category is whichever group it holds the most stacks of. A chest that's mostly
    /// uncategorized items (not in any group) is a junk chest. Items in the open chest that don't match
    /// its category move to a nearby chest of their own category, or failing that to a junk chest, or
    /// failing that stay where they are. Empty chests are never claimed.
    /// </summary>
    internal static class Tidier
    {
        /// <summary>Category key for chests that are mostly uncategorized items.</summary>
        private const string Junk = "\u0001junk";

        public static void TidyChest(Container opened)
        {
            Player player = Player.m_localPlayer;
            if (player == null || opened == null || !ChestFinder.EnsureOwner(opened))
                return;

            ItemGroups.ReloadIfChanged();
            ChestFinder.Invalidate();
            var others = ChestFinder.GetNearby(player).Where(c => c != opened).ToList();
            // Work out each chest's category once, before anything moves.
            var categories = others.ToDictionary(c => c, c => Category(c.GetInventory()));

            Inventory from = opened.GetInventory();
            string own = Category(from);
            var touched = new HashSet<Container>();
            int moved = 0;
            int stayed = 0;

            if (own != null)
            {
                foreach (ItemDrop.ItemData item in from.GetAllItems().ToList())
                {
                    string category = ItemGroups.Of(item) ?? Junk;
                    if (category == own)
                        continue;

                    moved += MoveToCategory(item, category, from, categories, touched);
                    // Fall back to a junk chest - unless this already is one; no point shuffling junk around.
                    if (category != Junk && own != Junk && from.ContainsItem(item))
                        moved += MoveToCategory(item, Junk, from, categories, touched);
                    if (from.ContainsItem(item))
                        stayed++;
                }
            }

            from.Changed();
            Stacker.Sort(from);
            foreach (Container c in touched)
            {
                Stacker.Sort(c.GetInventory());
                InventoryGui.instance.m_moveItemEffects.Create(c.transform.position, Quaternion.identity);
            }

            string message;
            if (moved > 0)
                message = $"Moved {moved} items into {touched.Count} {(touched.Count == 1 ? "chest" : "chests")}";
            else if (stayed > 0)
                message = "Sorted - nothing had a better chest to go to";
            else
                message = "Sorted - everything here belongs";
            if (moved > 0 && stayed > 0)
                message += $"\n{stayed} {(stayed == 1 ? "stack" : "stacks")} had no matching or junk chest, so stayed";
            player.Message(MessageHud.MessageType.Center, message);
        }

        /// <summary>Move an item into nearby chests of the given category, the most-dedicated first.</summary>
        private static int MoveToCategory(ItemDrop.ItemData item, string category, Inventory from,
            Dictionary<Container, string> categories, HashSet<Container> touched)
        {
            int moved = 0;
            var homes = categories
                .Where(kv => kv.Value == category)
                .Select(kv => kv.Key)
                .OrderByDescending(c => CategoryCount(c.GetInventory(), category))
                .ToList();
            foreach (Container c in homes)
            {
                moved += Stacker.MoveInto(c, item, from, touched);
                if (!from.ContainsItem(item))
                    break;
            }
            return moved;
        }

        /// <summary>
        /// The category with the most stacks in the chest (uncategorized items count as Junk),
        /// or null if the chest is empty.
        /// </summary>
        private static string Category(Inventory inv)
        {
            var counts = new Dictionary<string, int>();
            foreach (ItemDrop.ItemData item in inv.GetAllItems())
            {
                string category = ItemGroups.Of(item) ?? Junk;
                counts.TryGetValue(category, out int n);
                counts[category] = n + 1;
            }
            if (counts.Count == 0)
                return null;

            // Ties go to whichever group comes first in the groups file; Junk loses ties.
            return counts
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key == Junk ? int.MaxValue : ItemGroups.RankOf(kv.Key))
                .First().Key;
        }

        private static int CategoryCount(Inventory inv, string category) =>
            inv.GetAllItems().Count(i => (ItemGroups.Of(i) ?? Junk) == category);
    }

    /// <summary>Adds a "Tidy" button next to Take All / Stack in the chest window.</summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Awake))]
    internal static class InventoryGui_Awake_TidyButton_Patch
    {
        private static void Postfix(InventoryGui __instance)
        {
            if (!Plugin.TidyButton.Value)
                return;

            Button stack = __instance.m_stackAllButton;
            Button takeAll = __instance.m_takeAllButton;
            if (stack == null)
                return;

            GameObject clone = Object.Instantiate(stack.gameObject, stack.transform.parent);
            clone.name = "NearbyChests_Tidy";

            // Drop the controller shortcut, or one gamepad press would trigger Stack and Tidy together.
            foreach (UIGamePad pad in clone.GetComponentsInChildren<UIGamePad>(true))
            {
                if (pad.m_hint != null)
                    Object.Destroy(pad.m_hint);
                Object.Destroy(pad);
            }

            // Place it one button-width past Stack, using the same spacing as Take All -> Stack.
            var stackRect = (RectTransform)stack.transform;
            var cloneRect = (RectTransform)clone.transform;
            Vector2 step = takeAll != null
                ? stackRect.anchoredPosition - ((RectTransform)takeAll.transform).anchoredPosition
                : new Vector2(stackRect.rect.width + 10f, 0f);
            cloneRect.anchoredPosition = stackRect.anchoredPosition + step;

            TMP_Text label = clone.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = "Tidy";

            Button button = clone.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() =>
            {
                Player player = Player.m_localPlayer;
                if (player == null || player.IsTeleporting() || __instance.m_currentContainer == null)
                    return;
                __instance.SetupDragItem(null, null, 1);
                Tidier.TidyChest(__instance.m_currentContainer);
            });
        }
    }
}
