using System.Collections.Generic;
using UnityEngine;

namespace NearbyChests
{
    /// <summary>
    /// Tracks every loaded container and answers "which chests near the player can I use?".
    /// Results are cached briefly because the crafting and build menus ask many times per frame.
    /// </summary>
    internal static class ChestFinder
    {
        private const float CacheSeconds = 1f;

        private static readonly HashSet<Container> Known = new HashSet<Container>();
        private static readonly List<Container> Nearby = new List<Container>();
        private static readonly Dictionary<string, int> CountCache = new Dictionary<string, int>();
        private static float _cachedAt = -1000f;
        private static Vector3 _cachedPos;
        private static float _cachedRange = -1f;

        public static void Register(Container container) => Known.Add(container);

        /// <summary>Drop cached results so the next query rescans.</summary>
        public static void Invalidate()
        {
            _cachedAt = -1000f;
            CountCache.Clear();
        }

        /// <summary>A chest's contents changed; the list of chests is still valid.</summary>
        public static void InvalidateCounts() => CountCache.Clear();

        /// <summary>Chests within <paramref name="range"/> meters, closest first.</summary>
        public static List<Container> GetNearby(Player player, float range)
        {
            Vector3 pos = player.transform.position;
            if (Time.time - _cachedAt < CacheSeconds && range == _cachedRange
                && (pos - _cachedPos).sqrMagnitude < 1f)
                return Nearby;

            _cachedAt = Time.time;
            _cachedPos = pos;
            _cachedRange = range;
            CountCache.Clear();
            Nearby.Clear();
            Known.RemoveWhere(c => c == null);

            float rangeSq = range * range;
            long playerId = Game.instance.GetPlayerProfile().GetPlayerID();
            foreach (Container c in Known)
            {
                if (IsUsable(c, pos, rangeSq, playerId))
                    Nearby.Add(c);
            }
            Nearby.Sort((a, b) =>
                (a.transform.position - pos).sqrMagnitude.CompareTo((b.transform.position - pos).sqrMagnitude));
            return Nearby;
        }

        private static bool IsUsable(Container c, Vector3 playerPos, float rangeSq, long playerId)
        {
            if (c == null || c.m_nview == null || !c.m_nview.IsValid() || c.GetInventory() == null)
                return false;
            if ((c.transform.position - playerPos).sqrMagnitude > rangeSq)
                return false;

            // Never touch the Obliterator or gravestones.
            if (c.GetComponentInParent<Incinerator>() != null || c.GetComponentInParent<TombStone>() != null)
                return false;

            bool isVehicle = c.m_wagon != null || c.GetComponentInParent<Ship>() != null;
            if (isVehicle)
            {
                if (!Plugin.IncludeCartsAndShips.Value)
                    return false;
            }
            else if (c.m_piece == null || !c.m_piece.IsPlacedByPlayer())
            {
                // Dungeon loot chests and other world containers.
                return false;
            }

            if (c.m_privacy == Container.PrivacySetting.Private && c.m_piece == null)
                return false;
            if (!c.CheckAccess(playerId))
                return false;
            if (c.m_checkGuardStone && !PrivateArea.CheckAccess(c.transform.position, 0f, false))
                return false;

            // Someone else has it open.
            if (!c.m_nview.IsOwner() && c.m_nview.GetZDO().GetInt(ZDOVars.s_inUse) == 1)
                return false;

            return true;
        }

        /// <summary>
        /// Make sure we own the chest's network object so our changes save and sync.
        /// Returns false if another player is using it.
        /// </summary>
        public static bool EnsureOwner(Container c)
        {
            if (c.m_nview.IsOwner())
                return true;
            if (c.m_nview.GetZDO().GetInt(ZDOVars.s_inUse) == 1)
                return false;
            c.m_nview.ClaimOwnership();
            return c.m_nview.IsOwner();
        }

        public static int Count(Player player, string name, int quality, bool matchWorldLevel)
        {
            List<Container> chests = GetNearby(player, Plugin.CraftingRange.Value);
            string key = name + "|" + quality + "|" + matchWorldLevel;
            if (CountCache.TryGetValue(key, out int cached))
                return cached;

            int total = 0;
            foreach (Container c in chests)
                total += c.GetInventory().CountItems(name, quality, matchWorldLevel);
            CountCache[key] = total;
            return total;
        }

        public static bool Have(Player player, string name, bool matchWorldLevel)
        {
            foreach (Container c in GetNearby(player, Plugin.CraftingRange.Value))
            {
                if (c.GetInventory().HaveItem(name, matchWorldLevel))
                    return true;
            }
            return false;
        }

        public static ItemDrop.ItemData Find(Player player, string name, int quality)
        {
            foreach (Container c in GetNearby(player, Plugin.CraftingRange.Value))
            {
                ItemDrop.ItemData item = c.GetInventory().GetItem(name, quality);
                if (item != null)
                    return item;
            }
            return null;
        }

        /// <summary>Remove up to <paramref name="amount"/> matching items from nearby chests, closest first.</summary>
        public static void Remove(Player player, string name, int amount, int quality, bool worldLevelBased)
        {
            foreach (Container c in GetNearby(player, Plugin.CraftingRange.Value))
            {
                if (amount <= 0)
                    break;
                Inventory inv = c.GetInventory();
                int available = inv.CountItems(name, quality, worldLevelBased);
                if (available <= 0 || !EnsureOwner(c))
                    continue;
                int take = Mathf.Min(available, amount);
                inv.RemoveItem(name, take, quality, worldLevelBased);
                amount -= take;
            }
            CountCache.Clear();
        }
    }
}
