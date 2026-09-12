using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NearbyChests
{
    /// <summary>
    /// The Tidy button cleans out the chest you have open, plus the chest-category rules it shares
    /// with Stack.
    ///
    /// Each chest's category is whichever group it holds the most stacks of. A chest that's mostly
    /// uncategorized items (not in any group) is a junk chest, and a chest holding exactly two groups is
    /// a shared chest. Items in the open chest that don't match its category move to, in order:
    ///   1. a chest of their own group (dedicated chests first, then shared chests holding the group),
    ///   2. an empty chest, which becomes that group's chest,
    ///   3. a single-group chest with room, which becomes a shared chest (related groups preferred),
    ///   4. a junk chest.
    /// Anything left over stays. The second group in a shared chest stays put if it has no better home,
    /// so shared chests don't bounce items back and forth.
    /// </summary>
    internal static class Tidier
    {
        /// <summary>Category key for items that aren't in any group.</summary>
        internal const string Junk = "\u0001junk";

        public static void TidyChest(Container opened)
        {
            Player player = Player.m_localPlayer;
            if (player == null || opened == null || !ChestFinder.EnsureOwner(opened))
                return;

            ItemGroups.ReloadIfChanged();
            ChestFinder.Invalidate();
            var others = ChestFinder.GetNearby(player, Plugin.StackingRange.Value).Where(c => c != opened).ToList();

            Inventory from = opened.GetInventory();
            string own = Category(from);
            var touched = new HashSet<Container>();
            int moved = 0;
            int stayed = 0;

            if (own != null)
            {
                foreach (ItemDrop.ItemData item in from.GetAllItems().ToList())
                {
                    string group = GroupOf(item);
                    if (group == own)
                        continue;

                    // 1-2: a proper home.
                    moved += MoveToGroupChest(item, group, from, others, touched);
                    if (from.ContainsItem(item))
                        moved += MoveToEmptyChest(item, from, others, touched);
                    if (!from.ContainsItem(item))
                        continue;

                    // No proper home: if this is a two-group chest, the second group is allowed to stay.
                    if (Plugin.ShareChests.Value && GroupsIn(from).Count <= 2)
                        continue;

                    // 3-4: share another chest, or the junk chest (unless this already is one).
                    if (Plugin.ShareChests.Value)
                        moved += MoveToSharedChest(item, group, from, others, touched);
                    if (group != Junk && own != Junk && from.ContainsItem(item))
                        moved += MoveToJunkChest(item, from, others, touched);
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
                message += $"\n{stayed} {(stayed == 1 ? "stack" : "stacks")} had nowhere else to go, so stayed";
            player.Message(MessageHud.MessageType.Center, message);
        }

        internal static string GroupOf(ItemDrop.ItemData item) => ItemGroups.Of(item) ?? Junk;

        /// <summary>
        /// Chests that are home to the group: dedicated chests (the group is their main category) with
        /// the most of it first, then shared chests that hold the group.
        /// </summary>
        private static int MoveToGroupChest(ItemDrop.ItemData item, string group, Inventory from,
            List<Container> others, HashSet<Container> touched)
        {
            var homes = others
                .Select(c => new { Chest = c, Inv = c.GetInventory() })
                .Where(x => Category(x.Inv) == group)
                .OrderByDescending(x => CategoryCount(x.Inv, group))
                .Select(x => x.Chest)
                .ToList();
            if (Plugin.ShareChests.Value)
            {
                homes.AddRange(others.Where(c => !homes.Contains(c)
                    && GroupsIn(c.GetInventory()) is var g && g.Count == 2 && g.Contains(group)));
            }
            return MoveIntoFirst(item, from, homes, touched);
        }

        /// <summary>Start a new chest for the item's group in the nearest empty chest.</summary>
        internal static int MoveToEmptyChest(ItemDrop.ItemData item, Inventory from,
            IEnumerable<Container> candidates, HashSet<Container> touched) =>
            MoveIntoFirst(item, from, candidates.Where(c => c.GetInventory().NrOfItems() == 0).ToList(), touched);

        /// <summary>
        /// Add the item's group to a chest that holds only one other group, turning it into a shared
        /// chest. Prefers the most closely related group (nearest in the groups file), then the chest
        /// with the most free space.
        /// </summary>
        internal static int MoveToSharedChest(ItemDrop.ItemData item, string group, Inventory from,
            IEnumerable<Container> candidates, HashSet<Container> touched)
        {
            // Uncategorized items belong in a junk chest, not as a proper chest's second group.
            if (group == Junk)
                return 0;
            var partners = candidates
                .Select(c => new { Chest = c, Inv = c.GetInventory() })
                .Select(x => new { x.Chest, x.Inv, Groups = GroupsIn(x.Inv) })
                .Where(x => x.Groups.Count == 1 && !x.Groups.Contains(Junk) && !x.Groups.Contains(group)
                    && x.Inv.HaveEmptySlot())
                .OrderBy(x => Relatedness(group, x.Groups.First()))
                .ThenByDescending(x => x.Inv.GetEmptySlots())
                .Select(x => x.Chest)
                .ToList();
            return MoveIntoFirst(item, from, partners, touched);
        }

        private static int MoveToJunkChest(ItemDrop.ItemData item, Inventory from,
            List<Container> others, HashSet<Container> touched)
        {
            var junk = others
                .Where(c => Category(c.GetInventory()) == Junk)
                .OrderByDescending(c => CategoryCount(c.GetInventory(), Junk))
                .ToList();
            return MoveIntoFirst(item, from, junk, touched);
        }

        private static int MoveIntoFirst(ItemDrop.ItemData item, Inventory from, List<Container> chests,
            HashSet<Container> touched)
        {
            int moved = 0;
            foreach (Container c in chests)
            {
                moved += Stacker.MoveInto(c, item, from, touched);
                if (!from.ContainsItem(item))
                    break;
            }
            return moved;
        }

        /// <summary>How far apart two groups are in the groups file; neighbours are most related.</summary>
        private static int Relatedness(string a, string b)
        {
            int ra = ItemGroups.RankOf(a), rb = ItemGroups.RankOf(b);
            if (ra == int.MaxValue || rb == int.MaxValue)
                return 1000;
            return System.Math.Abs(ra - rb);
        }

        /// <summary>The distinct groups in a chest (uncategorized items count as Junk).</summary>
        internal static HashSet<string> GroupsIn(Inventory inv)
        {
            var groups = new HashSet<string>();
            foreach (ItemDrop.ItemData item in inv.GetAllItems())
                groups.Add(GroupOf(item));
            return groups;
        }

        /// <summary>
        /// The category with the most stacks in the chest (uncategorized items count as Junk),
        /// or null if the chest is empty.
        /// </summary>
        internal static string Category(Inventory inv)
        {
            var counts = new Dictionary<string, int>();
            foreach (ItemDrop.ItemData item in inv.GetAllItems())
            {
                string group = GroupOf(item);
                counts.TryGetValue(group, out int n);
                counts[group] = n + 1;
            }
            if (counts.Count == 0)
                return null;

            // Ties go to whichever group comes first in the groups file; Junk loses ties.
            return counts
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key == Junk ? int.MaxValue : ItemGroups.RankOf(kv.Key))
                .First().Key;
        }

        private static int CategoryCount(Inventory inv, string group) =>
            inv.GetAllItems().Count(i => GroupOf(i) == group);
    }

    /// <summary>Adds a small Tidy icon button to the chest window's title bar, just left of Place Stacks.</summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Awake))]
    internal static class InventoryGui_Awake_TidyButton_Patch
    {
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
