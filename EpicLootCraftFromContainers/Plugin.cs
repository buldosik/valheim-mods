using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using CraftFromContainers;
using UnityEngine;

namespace EpicLootCraftFromContainers
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("aedenthorn.CraftFromContainers", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("randyknapp.mods.epicloot", BepInDependency.DependencyFlags.HardDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "buldosik.EpicLootCraftFromContainers";
        public const string PluginName = "EpicLoot CraftFromContainers Bridge";
        public const string PluginVersion = "0.1.0";

        private const string ProviderId = PluginGuid;
        private static ManualLogSource Log = null!;
        private bool _providerRegistered;
        private static MethodInfo? _unregisterProvider;

        private void Awake()
        {
            Log = Logger;

            try
            {
                _providerRegistered = RegisterEpicLootProvider();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to register EpicLoot inventory provider: {ex}");
            }
        }

        private void OnDestroy()
        {
            try
            {
                if (_providerRegistered)
                    _unregisterProvider?.Invoke(null, new object[] { ProviderId });
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to unregister EpicLoot inventory provider: {ex.GetBaseException().Message}");
            }
        }

        private static bool RegisterEpicLootProvider()
        {
            // EpicLoot 0.14.8 targets net481; keep this net48 bridge late-bound.
            Type? api = Type.GetType("EpicLoot.API, EpicLoot");
            if (api == null)
            {
                Log.LogError("EpicLoot.API was not found.");
                return false;
            }

            Type[] signature =
            {
                typeof(string),
                typeof(Func<List<ItemDrop.ItemData>>),
                typeof(Func<string, int>),
                typeof(Func<string, int, int>),
                typeof(Func<ItemDrop.ItemData, int, int>)
            };

            MethodInfo? register = api.GetMethod(
                "RegisterInventoryProvider",
                BindingFlags.Public | BindingFlags.Static,
                null,
                signature,
                null);

            if (register == null || register.ReturnType != typeof(bool))
            {
                Log.LogError("EpicLoot RegisterInventoryProvider API with the expected signature was not found.");
                return false;
            }

            bool ok = (bool)register.Invoke(null, new object[]
            {
                ProviderId,
                new Func<List<ItemDrop.ItemData>>(GetItems),
                new Func<string, int>(CountItem),
                new Func<string, int, int>(RemoveItem),
                new Func<ItemDrop.ItemData, int, int>(RemoveExactItem)
            });

            if (!ok)
            {
                Log.LogError("EpicLoot rejected the CraftFromContainers inventory provider registration.");
                return false;
            }

            _unregisterProvider = api.GetMethod(
                "UnregisterInventoryProvider",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);

            Log.LogInfo("Registered CraftFromContainers as an EpicLoot enchanting material provider.");
            return true;
        }

        private static bool CfcIsActive()
        {
            try
            {
                if (BepInExPlugin.modEnabled?.Value == false)
                    return false;

                return BepInExPlugin.AllowByKey();
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Could not read CraftFromContainers enabled state: {ex.GetBaseException().Message}");
            }

            return true;
        }

        private static bool LeaveOneEnabled()
        {
            try
            {
                return BepInExPlugin.leaveOne?.Value ?? false;
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Could not read CraftFromContainers LeaveOne setting: {ex.GetBaseException().Message}");
            }

            return false;
        }

        private static List<Container> NearbyContainers()
        {
            Player player = Player.m_localPlayer;
            if (player == null || !CfcIsActive())
                return new List<Container>();

            try
            {
                // IMPORTANT:
                // Container discovery/range/access/type filtering is owned by CraftFromContainers.
                // This means every container supported by CFC is automatically supported here.
                return BepInExPlugin.GetNearbyContainers(player.transform.position)
                    ?? new List<Container>();
            }
            catch (Exception ex)
            {
                Log.LogWarning($"CraftFromContainers GetNearbyContainers failed: {ex.GetBaseException().Message}");
                return new List<Container>();
            }
        }

        private static bool IsMaterial(ItemDrop.ItemData? item)
        {
            return item?.m_shared != null &&
                   item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Material;
        }

        private static List<ItemDrop.ItemData> GetItems()
        {
            var result = new List<ItemDrop.ItemData>();
            int reserve = LeaveOneEnabled() ? 1 : 0;

            foreach (Container container in NearbyContainers())
            {
                Inventory? inventory = SafeGetInventory(container);
                if (inventory == null)
                    continue;

                // GetInventory() is intentional:
                // custom-container mods such as RossItemDrawers can patch this and expose
                // their virtual inventory exactly the same way CFC sees it.
                // Clone first, then apply LeaveOne once per material name per container.
                // CFC reserves one matching item per container, not one per stack.
                var clones = inventory.GetAllItems()
                    .Where(IsMaterial)
                    .Select(item => item.Clone())
                    .ToList();

                if (reserve > 0)
                {
                    foreach (var group in clones.GroupBy(i => i.m_shared.m_name))
                    {
                        ItemDrop.ItemData first = group.FirstOrDefault(i => i.m_stack > 0);
                        if (first != null)
                            first.m_stack -= 1;
                    }
                }

                result.AddRange(clones.Where(i => i.m_stack > 0));
            }

            return result;
        }

        private static int CountItem(string name)
        {
            if (string.IsNullOrEmpty(name))
                return 0;

            int reserve = LeaveOneEnabled() ? 1 : 0;
            int total = 0;

            foreach (Container container in NearbyContainers())
            {
                Inventory? inventory = SafeGetInventory(container);
                if (inventory == null)
                    continue;

                int count = inventory.CountItems(name, -1, true);
                total += Math.Max(0, count - reserve);
            }

            return total;
        }

        private static int RemoveItem(string name, int amount)
        {
            if (string.IsNullOrEmpty(name) || amount <= 0)
                return 0;

            int remaining = amount;
            int reserve = LeaveOneEnabled() ? 1 : 0;

            foreach (Container container in NearbyContainers())
            {
                if (remaining <= 0)
                    break;

                Inventory? inventory = SafeGetInventory(container);
                if (inventory == null)
                    continue;

                int count = inventory.CountItems(name, -1, true);
                int available = Math.Max(0, count - reserve);
                int take = Math.Min(remaining, available);

                if (take <= 0)
                    continue;

                inventory.RemoveItem(name, take, -1, true);
                SaveContainer(container);
                remaining -= take;
            }

            return amount - remaining;
        }

        private static int RemoveExactItem(ItemDrop.ItemData requestedItem, int amount)
        {
            if (requestedItem?.m_shared == null || amount <= 0)
                return 0;

            // EpicLoot's provider API supports exact-item removal. Since GetItems() returns
            // clones, match by the item identity fields EpicLoot costs care about rather than
            // object reference.
            string name = requestedItem.m_shared.m_name;
            int quality = requestedItem.m_quality;
            int worldLevel = requestedItem.m_worldLevel;

            int remaining = amount;
            int reserve = LeaveOneEnabled() ? 1 : 0;

            foreach (Container container in NearbyContainers())
            {
                if (remaining <= 0)
                    break;

                Inventory? inventory = SafeGetInventory(container);
                if (inventory == null)
                    continue;

                List<ItemDrop.ItemData> matches = inventory.GetAllItems()
                    .Where(i =>
                        IsMaterial(i) &&
                        i.m_shared.m_name == name &&
                        i.m_quality == quality &&
                        i.m_worldLevel == worldLevel)
                    .ToList();

                if (matches.Count == 0)
                    continue;

                int totalMatching = matches.Sum(i => i.m_stack);
                int available = Math.Max(0, totalMatching - reserve);
                int takeFromContainer = Math.Min(remaining, available);

                if (takeFromContainer <= 0)
                    continue;

                int leftHere = takeFromContainer;

                foreach (ItemDrop.ItemData item in matches)
                {
                    if (leftHere <= 0)
                        break;

                    int take = Math.Min(leftHere, item.m_stack);
                    if (take <= 0)
                        continue;

                    // Use Valheim's public Inventory API instead of touching
                    // private inventory internals. This also gives custom
                    // container mods a chance to intercept normal removal.
                    if (inventory.RemoveItem(item, take))
                        leftHere -= take;
                }

                SaveContainer(container);

                int removedHere = takeFromContainer - leftHere;
                remaining -= removedHere;
            }

            return amount - remaining;
        }

        private static Inventory? SafeGetInventory(Container container)
        {
            try
            {
                return container?.GetInventory();
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed reading container inventory: {ex.GetBaseException().Message}");
                return null;
            }
        }

        private static void SaveContainer(Container container)
        {
            try
            {
                // Container.Save is private in the supplied Valheim assembly.
                // Keep the existing save path until multiplayer persistence is addressed.
                MethodInfo? save = typeof(Container).GetMethod(
                    "Save",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                save?.Invoke(container, null);
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed saving container: {ex.GetBaseException().Message}");
            }
        }
    }
}
