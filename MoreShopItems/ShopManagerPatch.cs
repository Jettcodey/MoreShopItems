using System.Collections.Generic;
using System.Threading.Tasks;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using MoreShopItems.Compatability;

namespace MoreShopItems;

[HarmonyPatch(typeof(ShopManager))]
internal static class ShopManagerPatch
{
    private static readonly AccessTools.FieldRef<ShopManager, int> itemSpawnTargetAmount_ref = AccessTools.FieldRefAccess<ShopManager, int>("itemSpawnTargetAmount");
    private static readonly AccessTools.FieldRef<ShopManager, int> itemConsumablesAmount_ref = AccessTools.FieldRefAccess<ShopManager, int>("itemConsumablesAmount");
    private static readonly AccessTools.FieldRef<ShopManager, int> itemUpgradesAmount_ref = AccessTools.FieldRefAccess<ShopManager, int>("itemUpgradesAmount");
    private static readonly AccessTools.FieldRef<ShopManager, int> itemHealthPacksAmount_ref = AccessTools.FieldRefAccess<ShopManager, int>("itemHealthPacksAmount");
    private static GameObject? shelf;
    internal static bool isMoreUpgrades = false;

    [HarmonyPrefix]
    [HarmonyPatch("ShopInitialize")]
    private static void AdjustItems()
    {
        if (!(RunManager.instance.levelCurrent.ResourcePath == "Shop") || !((Object)StatsManager.instance != null) || !SemiFunc.IsMasterClient() && SemiFunc.IsMultiplayer())
            return;
        Dictionary<string, ConfigEntry<int>> intConfigEntries = Plugin.Instance.intConfigEntries;
        Dictionary<string, ConfigEntry<bool>> boolConfigEntries = Plugin.Instance.boolConfigEntries;
        Plugin.Logger.LogInfo((object)("Override modded items = " + boolConfigEntries["Override Modded Items"].Value.ToString()));
        foreach (Item obj in StatsManager.instance.itemDictionary.Values)
        {
            int maxInShop = -2;
            int maxPurchaseAmount = -2;
            switch (obj.itemType)
            {
                case SemiFunc.itemType.drone:
                    if (intConfigEntries["Max Drones In Shop"].Value != -1)
                    {
                        maxInShop = intConfigEntries["Max Drones In Shop"].Value;
                        maxPurchaseAmount = intConfigEntries["Max Drone Purchase Amount"].Value;
                        break;
                    }
                    break;
                case SemiFunc.itemType.orb:
                    if (intConfigEntries["Max Orbs In Shop"].Value != -1)
                    {
                        maxInShop = intConfigEntries["Max Orbs In Shop"].Value;
                        maxPurchaseAmount = intConfigEntries["Max Orb Purchase Amount"].Value;
                        break;
                    }
                    break;
                case SemiFunc.itemType.item_upgrade:
                    if (intConfigEntries["Max Upgrades In Shop"].Value != -1)
                    {
                        maxInShop = intConfigEntries["Max Upgrades In Shop"].Value;
                        maxPurchaseAmount = intConfigEntries["Max Upgrade Purchase Amount"].Value;
                        break;
                    }
                    break;
                case SemiFunc.itemType.power_crystal:
                    if (intConfigEntries["Max Crystals In Shop"].Value != -1)
                    {
                        maxInShop = intConfigEntries["Max Crystals In Shop"].Value;
                        maxPurchaseAmount = intConfigEntries["Max Crystal Purchase Amount"].Value;
                        break;
                    }
                    break;
                case SemiFunc.itemType.grenade:
                    if (intConfigEntries["Max Grenades In Shop"].Value != -1)
                    {
                        maxInShop = intConfigEntries["Max Grenades In Shop"].Value;
                        maxPurchaseAmount = intConfigEntries["Max Grenade Purchase Amount"].Value;
                        break;
                    }
                    break;
                case SemiFunc.itemType.melee:
                    if (intConfigEntries["Max Melee Weapons In Shop"].Value != -1)
                    {
                        maxInShop = intConfigEntries["Max Melee Weapons In Shop"].Value;
                        maxPurchaseAmount = intConfigEntries["Max Melee Weapon Purchase Amount"].Value;
                        break;
                    }
                    break;
                case SemiFunc.itemType.healthPack:
                    if (intConfigEntries["Max Health-Packs In Shop"].Value != -1)
                    {
                        maxInShop = intConfigEntries["Max Health-Packs In Shop"].Value;
                        maxPurchaseAmount = intConfigEntries["Max Health-Pack Purchase Amount"].Value;
                        break;
                    }
                    break;
                case SemiFunc.itemType.gun:
                    if (intConfigEntries["Max Guns In Shop"].Value != -1)
                    {
                        maxInShop = intConfigEntries["Max Guns In Shop"].Value;
                        maxPurchaseAmount = intConfigEntries["Max Gun Purchase Amount"].Value;
                        break;
                    }
                    break;
                case SemiFunc.itemType.tracker:
                    if (intConfigEntries["Max Trackers In Shop"].Value != -1)
                    {
                        maxInShop = intConfigEntries["Max Trackers In Shop"].Value;
                        maxPurchaseAmount = intConfigEntries["Max Tracker Purchase Amount"].Value;
                        break;
                    }
                    break;
                case SemiFunc.itemType.mine:
                    if (intConfigEntries["Max Mines In Shop"].Value != -1)
                    {
                        maxInShop = intConfigEntries["Max Mines In Shop"].Value;
                        maxPurchaseAmount = intConfigEntries["Max Mine Purchase Amount"].Value;
                        break;
                    }
                    break;
                case SemiFunc.itemType.cart:
                    if (intConfigEntries["Max Carts In Shop"].Value != -1)
                    {
                        maxInShop = intConfigEntries["Max Carts In Shop"].Value;
                        maxPurchaseAmount = intConfigEntries["Max Cart Purchase Amount"].Value;
                    }
                    break;

                case SemiFunc.itemType.pocket_cart:
                    if (intConfigEntries["Max Pocket Carts In Shop"].Value != -1)
                    {
                        maxInShop = intConfigEntries["Max Pocket Carts In Shop"].Value;
                        maxPurchaseAmount = intConfigEntries["Max Pocket Cart Purchase Amount"].Value;
                    }
                    break;
                case SemiFunc.itemType.tool:
                    if (intConfigEntries["Max Tools In Shop"].Value != -1)
                    {
                        maxInShop = intConfigEntries["Max Tools In Shop"].Value;
                        maxPurchaseAmount = intConfigEntries["Max Tool Purchase Amount"].Value;
                    }
                    break;
                default:
                    continue;
            }
            bool flag = obj.itemType == SemiFunc.itemType.item_upgrade;
            if (maxInShop != -2)
            {
                if (boolConfigEntries["Override Modded Items"].Value)
                {
                    if (boolConfigEntries["Override Single-Use Upgrades"].Value & flag)
                        ShopManagerPatch.SetItemValues(obj, maxInShop, maxPurchaseAmount);
                    else if (!obj.maxPurchase)
                        ShopManagerPatch.SetItemValues(obj, maxInShop, maxPurchaseAmount);
                    else if (!flag)
                        ShopManagerPatch.SetItemValues(obj, maxInShop, maxPurchaseAmount);
                }
                else if ((!MoreUpgradesMOD.isLoaded() && !(NikkisUpgradesMOD.isLoaded()) || !obj.itemAssetName.Contains("Modded")) && !(VanillaUpgradesMOD.isLoaded() & flag))
                {
                    if (boolConfigEntries["Override Single-Use Upgrades"].Value & flag)
                        ShopManagerPatch.SetItemValues(obj, maxInShop, maxPurchaseAmount);
                    else if (flag && !obj.maxPurchase)
                        ShopManagerPatch.SetItemValues(obj, maxInShop, maxPurchaseAmount);
                    else if (!flag)
                        ShopManagerPatch.SetItemValues(obj, maxInShop, maxPurchaseAmount);
                }
            }
        }
    }

    private static void SetItemValues(Item item, int maxInShop, int maxPurchaseAmount)
    {
        item.maxAmountInShop = item.maxAmount = maxInShop;
        item.maxPurchase = maxPurchaseAmount > 0;
        item.maxPurchaseAmount = maxPurchaseAmount;
    }

    [PunRPC]
    public static void SetParent(Transform parent, GameObject gameObj)
    {
        gameObj.transform.SetParent(parent);
    }

    [HarmonyPrefix]
    [HarmonyPatch("Awake")]
    private static void SetValues(ShopManager __instance)
    {
        ShopManagerPatch.itemConsumablesAmount_ref(__instance) = 150;
        ShopManagerPatch.itemUpgradesAmount_ref(__instance) = 110;
        ShopManagerPatch.itemHealthPacksAmount_ref(__instance) = 50;
        ShopManagerPatch.itemSpawnTargetAmount_ref(__instance) = 350;
    }

    [HarmonyPrefix]
    [HarmonyPatch("ShopInitialize")]
    private static void SpawnShelf()
    {
        if (!(RunManager.instance.levelCurrent.ResourcePath == "Shop") || !Plugin.Instance.boolConfigEntries["Spawn Additional Shelving"].Value)
            return;

        int maxShelvesToSpawn = Plugin.Instance.intConfigEntries["Max Additional Shelves In Shop"].Value;
        if (maxShelvesToSpawn <= 0)
            return;

        HashSet<string> usedLocations = new HashSet<string>();
        int shelvesSpawned = 0;

        for (int i = 0; i < maxShelvesToSpawn; i++)
        {
            bool spawnedThisIteration = false;

            if (!usedLocations.Contains("Soda"))
            {
                GameObject gameObject1 = GameObject.Find("Soda Shelf");
                GameObject gameObject2 = GameObject.Find("Module Switch BOT");
                if (gameObject1 != null && gameObject2 != null && !gameObject2.GetComponent<ModulePropSwitch>().ConnectedParent.activeSelf)
                {
                    if (SemiFunc.IsMultiplayer())
                    {
                        if (SemiFunc.IsMasterClient())
                        {
                            ShopManagerPatch.shelf = PhotonNetwork.Instantiate(Plugin.CustomItemShelf.name, gameObject1.transform.position, gameObject1.transform.rotation);
                            ShopManagerPatch.SetParent(gameObject2.transform, ShopManagerPatch.shelf);
                        }
                        gameObject1.SetActive(false);
                    }
                    else
                    {
                        ShopManagerPatch.shelf = Object.Instantiate<GameObject>(Plugin.CustomItemShelf, gameObject1.transform.position, gameObject1.transform.rotation, gameObject2.transform);
                        gameObject1.SetActive(false);
                    }
                    usedLocations.Add("Soda");
                    shelvesSpawned++;
                    spawnedThisIteration = true;
                }
            }

            if (spawnedThisIteration) continue;

            if (!usedLocations.Contains("Magazine"))
            {
                GameObject gameObject3 = GameObject.Find("Shop Magazine Stand (1)");
                GameObject gameObject4 = GameObject.Find("Shop Magazine Stand");
                GameObject gameObject5 = GameObject.Find("Module Switch (1) top");
                if (gameObject3 != null && gameObject5 != null && !gameObject5.GetComponent<ModulePropSwitch>().ConnectedParent.activeSelf)
                {
                    if (SemiFunc.IsMultiplayer())
                    {
                        if (SemiFunc.IsMasterClient())
                        {
                            ShopManagerPatch.shelf = PhotonNetwork.Instantiate(Plugin.CustomItemShelf.name, gameObject3.transform.position, gameObject3.transform.rotation * Quaternion.Euler(0.0f, 90f, 0.0f));
                            ShopManagerPatch.SetParent(gameObject5.transform, ShopManagerPatch.shelf);
                        }
                        gameObject3.SetActive(false);
                        if (gameObject4 != null)
                            gameObject4.SetActive(false);
                    }
                    else
                    {
                        ShopManagerPatch.shelf = Object.Instantiate<GameObject>(Plugin.CustomItemShelf, gameObject3.transform.position, gameObject3.transform.rotation * Quaternion.Euler(0.0f, 90f, 0.0f), gameObject5.transform.parent);
                        gameObject3.SetActive(false);
                        if (gameObject4 != null)
                            gameObject4.SetActive(false);
                    }
                    usedLocations.Add("Magazine");
                    shelvesSpawned++;
                    spawnedThisIteration = true;
                }
            }

            if (spawnedThisIteration) continue;

            if (!usedLocations.Contains("Candy"))
            {
                GameObject gameObject6 = GameObject.Find("Module Switch (2) left");
                GameObject gameObject7 = GameObject.Find("Candy Shelf");
                if (gameObject6 != null && gameObject7 != null && !gameObject6.GetComponent<ModulePropSwitch>().ConnectedParent.activeSelf)
                {
                    if (SemiFunc.IsMultiplayer())
                    {
                        if (SemiFunc.IsMasterClient())
                        {
                            ShopManagerPatch.shelf = PhotonNetwork.Instantiate(Plugin.CustomItemShelf.name, gameObject6.transform.position + gameObject6.transform.right * 0.5f - gameObject6.transform.forward * 0.8f, gameObject6.transform.rotation * Quaternion.Euler(0.0f, 180f, 0.0f));
                            ShopManagerPatch.SetParent(gameObject6.transform, ShopManagerPatch.shelf);
                        }
                        if (gameObject7 != null)
                            gameObject7.SetActive(false);
                    }
                    else
                    {
                        ShopManagerPatch.shelf = Object.Instantiate<GameObject>(Plugin.CustomItemShelf, gameObject6.transform.position + gameObject6.transform.right * 0.5f - gameObject6.transform.forward * 0.8f, gameObject6.transform.rotation * Quaternion.Euler(0.0f, 180f, 0.0f), gameObject6.transform.parent);
                        if (gameObject7 != null)
                            gameObject7.SetActive(false);
                    }
                    usedLocations.Add("Candy");
                    shelvesSpawned++;
                    spawnedThisIteration = true;
                }
            }

            // If no shelf was spawned in this full pass, it means no more locations are available.
            if (!spawnedThisIteration)
            {
                Plugin.Logger.LogInfo((object)$"No more available locations found. Stopping shelf spawn at {shelvesSpawned} shelves.");
                break; // Exit the loop
            }
        }

        if (shelvesSpawned > 0)
        {
            Plugin.Logger.LogInfo((object)$"Successfully spawned {shelvesSpawned} custom shelf/shelves!");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch("GetAllItemVolumesInScene")]
    private static void LogPotentialItems(ShopManager __instance)
    {
        if (Plugin.Instance.boolConfigEntries["Log Potential Items"].Value)
        {
            if (__instance == null) return;

            Plugin.Logger.LogInfo("--- Shop Initialization: Starting Item Scan ---");
            Task.Delay(2000).Wait(); // Wait for a moment to ensure items are loaded

            LogItemCollection(__instance.potentialItems, "Standard Items");
            LogItemCollection(__instance.potentialItemUpgrades, "Upgrade Items");
            LogItemCollection(__instance.potentialItemConsumables, "Consumables");
            LogItemCollection(__instance.potentialItemHealthPacks, "Health Packs");

            if (__instance.potentialSecretItems.Count > 0)
            {
                Plugin.Logger.LogInfo($"Found {__instance.potentialSecretItems.Count} potential Secret Items (grouped):");
                foreach (var entry in __instance.potentialSecretItems)
                {
                    Plugin.Logger.LogInfo($"  Category: {entry.Key}");
                    LogItemCollection(entry.Value, "", nested: true);
                }
            }
            else
            {
                Plugin.Logger.LogInfo("No potential Secret Items found.");
            }

            Plugin.Logger.LogInfo("--- Shop Initialization: Item Scan Complete ---");
        }
    }
    // make stuff easier to read in the console/logs
    private static void LogItemCollection(List<Item> items, string collectionName, bool nested = false)
    {
        if (items != null && items.Count > 0)
        {
            if (!string.IsNullOrEmpty(collectionName))
            {
                Plugin.Logger.LogInfo($"Found {items.Count} potential {collectionName}:");
            }

            int maxNameLength = 0;
            foreach (Item item in items)
            {
                if (item.itemName != null && item.itemName.Length > maxNameLength)
                {
                    maxNameLength = item.itemName.Length;
                }
            }

            maxNameLength += 3;

            foreach (Item item in items)
            {
                string prefix = nested ? "    - " : "  - ";
                Plugin.Logger.LogInfo($"{prefix}Name: {item.itemName.PadRight(maxNameLength)}Type: {item.itemType}");
            }
        }
        else if (!string.IsNullOrEmpty(collectionName))
        {
            Plugin.Logger.LogInfo($"No potential {collectionName} found.");
        }
    }
}