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

    /// <summary>
    /// Adds a small Tidy icon button to the chest window's title bar, just left of Place Stacks,
    /// with a tooltip explaining it.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Awake))]
    internal static class InventoryGui_Awake_TidyButton_Patch
    {
        private const string Tooltip =
            "Tidy: send items that don't belong in this chest to chests of their own kind, then sort it";

        private static void Postfix(InventoryGui __instance)
        {
            if (!Plugin.TidyButton.Value)
                return;

            Button stack = __instance.m_stackAllButton;
            if (stack == null)
                return;

            // Start from a copy of Place Stacks so the button background matches the game's style.
            GameObject clone = Object.Instantiate(stack.gameObject, stack.transform.parent);
            clone.name = "NearbyChests_Tidy";

            // Drop the controller shortcut, or one gamepad press would trigger Stack and Tidy together.
            foreach (UIGamePad pad in clone.GetComponentsInChildren<UIGamePad>(true))
            {
                if (pad.m_hint != null)
                    Object.Destroy(pad.m_hint);
                Object.Destroy(pad);
            }

            // Swap the text label for an icon, tinted like the text was.
            Color tint = new Color(1f, 0.85f, 0.55f);
            foreach (TMP_Text text in clone.GetComponentsInChildren<TMP_Text>(true))
            {
                tint = text.color;
                text.gameObject.SetActive(false);
            }

            var rect = (RectTransform)clone.transform;
            float size = ((RectTransform)stack.transform).rect.height;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size);

            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            var iconRect = (RectTransform)icon.transform;
            iconRect.SetParent(rect, false);
            iconRect.anchorMin = new Vector2(0.2f, 0.2f);
            iconRect.anchorMax = new Vector2(0.8f, 0.8f);
            iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
            var image = icon.GetComponent<Image>();
            image.sprite = TidyIcon.Create();
            image.color = tint;
            image.preserveAspect = true;
            image.raycastTarget = false;

            // Hover tooltip, borrowing the game's tooltip template from any existing tooltip.
            UITooltip template = __instance.GetComponentsInChildren<UITooltip>(true)
                .FirstOrDefault(t => t.m_tooltipPrefab != null);
            if (template != null)
            {
                UITooltip tooltip = clone.GetComponent<UITooltip>() ?? clone.AddComponent<UITooltip>();
                tooltip.m_tooltipPrefab = template.m_tooltipPrefab;
                tooltip.m_topic = "";
                tooltip.m_text = Tooltip;
            }

            // Final position is worked out the first time the chest window is on screen (see below),
            // once the UI has real sizes.
            TidyButtonPlacement.Button = rect;
            TidyButtonPlacement.Placed = false;

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

    /// <summary>
    /// Puts the Tidy icon just left of Place Stacks, vertically centred on it. Measured in world space
    /// so it lines up at any resolution or UI scale.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateContainer))]
    internal static class TidyButtonPlacement
    {
        internal static RectTransform Button;
        internal static bool Placed;

        private static readonly Vector3[] StackCorners = new Vector3[4];
        private static readonly Vector3[] ButtonCorners = new Vector3[4];

        private static void Postfix(InventoryGui __instance)
        {
            if (Placed || Button == null || __instance.m_container == null
                || !__instance.m_container.gameObject.activeInHierarchy)
                return;

            var stackRect = (RectTransform)__instance.m_stackAllButton.transform;
            Canvas.ForceUpdateCanvases();
            stackRect.GetWorldCorners(StackCorners); // 0 bottom-left, 1 top-left, 2 top-right, 3 bottom-right
            Button.GetWorldCorners(ButtonCorners);

            float buttonWidth = ButtonCorners[2].x - ButtonCorners[1].x;
            if (buttonWidth <= 0f)
                return; // Not laid out yet; try again next frame.

            float gap = buttonWidth * 0.15f;
            float centreX = StackCorners[0].x - gap - buttonWidth / 2f;
            float centreY = (StackCorners[0].y + StackCorners[1].y) / 2f;
            Button.position = new Vector3(centreX, centreY, stackRect.position.z);
            Placed = true;
        }
    }

    /// <summary>Draws the Tidy icon (three left-aligned bars, shortest at the bottom) at runtime.</summary>
    internal static class TidyIcon
    {
        private static Sprite _sprite;

        public static Sprite Create()
        {
            if (_sprite != null)
                return _sprite;

            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var clear = new Color(1f, 1f, 1f, 0f);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = clear;

            // Bars from top to bottom: full, three-quarter, half width. Texture rows start at the bottom.
            int[] lengths = { 28, 20, 12 };
            int barHeight = 5;
            int[] tops = { 26, 16, 6 };
            for (int b = 0; b < lengths.Length; b++)
            {
                for (int y = tops[b]; y < tops[b] + barHeight; y++)
                {
                    for (int x = 2; x < 2 + lengths[b]; x++)
                    {
                        // Soften the right-hand end of each bar a little.
                        float alpha = x == 1 + lengths[b] ? 0.5f : 1f;
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();

            _sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _sprite;
        }
    }
}
