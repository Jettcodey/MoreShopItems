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
        ShopManagerPatch.itemConsumablesAmount_ref(__instance) = 200;
        ShopManagerPatch.itemUpgradesAmount_ref(__instance) = 160;
        ShopManagerPatch.itemHealthPacksAmount_ref(__instance) = 60;
        ShopManagerPatch.itemSpawnTargetAmount_ref(__instance) = 450;
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
                GameObject sodaShelf = GameObject.Find("Soda Shelf");
                GameObject moduleBottom = GameObject.Find("Module Switch BOT");
                if (sodaShelf != null && moduleBottom != null && !moduleBottom.GetComponent<ModulePropSwitch>().ConnectedParent.activeSelf)
                {
                    if (SemiFunc.IsMultiplayer())
                    {
                        if (SemiFunc.IsMasterClient())
                        {
                            ShopManagerPatch.shelf = PhotonNetwork.Instantiate(Plugin.CustomItemShelf.name, sodaShelf.transform.position, sodaShelf.transform.rotation);
                            ShopManagerPatch.SetParent(moduleBottom.transform, ShopManagerPatch.shelf);
                        }
                        sodaShelf.SetActive(false);
                    }
                    else
                    {
                        ShopManagerPatch.shelf = Object.Instantiate<GameObject>(Plugin.CustomItemShelf, sodaShelf.transform.position, sodaShelf.transform.rotation, moduleBottom.transform);
                        sodaShelf.SetActive(false);
                    }
                    usedLocations.Add("Soda");
                    shelvesSpawned++;
                    spawnedThisIteration = true;
                }
            }

            if (spawnedThisIteration) continue;

            if (!usedLocations.Contains("Magazine"))
            {
                GameObject moduleTop = GameObject.Find("Module Switch (1) top");
                GameObject magazineStand_1 = GameObject.Find("Shop Magazine Stand (1)");
                GameObject magazineStand = GameObject.Find("Shop Magazine Stand");
                if (magazineStand_1 != null && moduleTop != null && !moduleTop.GetComponent<ModulePropSwitch>().ConnectedParent.activeSelf)
                {
                    if (SemiFunc.IsMultiplayer())
                    {
                        if (SemiFunc.IsMasterClient())
                        {
                            ShopManagerPatch.shelf = PhotonNetwork.Instantiate(Plugin.CustomItemShelf.name, magazineStand_1.transform.position, magazineStand_1.transform.rotation * Quaternion.Euler(0.0f, 90f, 0.0f));
                            ShopManagerPatch.SetParent(moduleTop.transform, ShopManagerPatch.shelf);
                        }
                        magazineStand_1.SetActive(false);
                        if (magazineStand != null)
                            magazineStand.SetActive(false);
                    }
                    else
                    {
                        ShopManagerPatch.shelf = Object.Instantiate<GameObject>(Plugin.CustomItemShelf, magazineStand_1.transform.position, magazineStand_1.transform.rotation * Quaternion.Euler(0.0f, 90f, 0.0f), moduleTop.transform.parent);
                        magazineStand_1.SetActive(false);
                        if (magazineStand != null)
                            magazineStand.SetActive(false);
                    }
                    usedLocations.Add("Magazine");
                    shelvesSpawned++;
                    spawnedThisIteration = true;
                }
            }

            if (spawnedThisIteration) continue;

            if (!usedLocations.Contains("Candy"))
            {
                GameObject moduleLeft = GameObject.Find("Module Switch (2) left");
                GameObject candyShelf = GameObject.Find("Candy Shelf");
                if (moduleLeft != null && candyShelf != null && !moduleLeft.GetComponent<ModulePropSwitch>().ConnectedParent.activeSelf)
                {
                    if (SemiFunc.IsMultiplayer())
                    {
                        if (SemiFunc.IsMasterClient())
                        {
                            ShopManagerPatch.shelf = PhotonNetwork.Instantiate(Plugin.CustomItemShelf.name, moduleLeft.transform.position + moduleLeft.transform.right * 0.5f - moduleLeft.transform.forward * 0.8f, moduleLeft.transform.rotation * Quaternion.Euler(0.0f, 180f, 0.0f));
                            ShopManagerPatch.SetParent(moduleLeft.transform, ShopManagerPatch.shelf);
                        }
                        if (candyShelf != null)
                            candyShelf.SetActive(false);
                    }
                    else
                    {
                        ShopManagerPatch.shelf = Object.Instantiate<GameObject>(Plugin.CustomItemShelf, moduleLeft.transform.position + moduleLeft.transform.right * 0.5f - moduleLeft.transform.forward * 0.8f, moduleLeft.transform.rotation * Quaternion.Euler(0.0f, 180f, 0.0f), moduleLeft.transform.parent);
                        if (candyShelf != null)
                            candyShelf.SetActive(false);
                    }
                    usedLocations.Add("Candy");
                    shelvesSpawned++;
                    spawnedThisIteration = true;
                }
            }

            if (spawnedThisIteration) continue;

            if (!usedLocations.Contains("Soda Machine"))
            {
                GameObject sodaMachine = GameObject.Find("Soda Machine (1)");
                GameObject moduleTop = GameObject.Find("Module Switch (1) top");
                GameObject shopOwner = GameObject.Find("Shop Owner");

                GameObject wallDoor = null;
                if (moduleTop != null)
                {
                    Transform connected = moduleTop.transform.Find("Connected");
                    if (connected != null)
                    {
                        wallDoor = connected.Find("Wall 01 - 1x1 - Door (3)")?.gameObject;
                    }
                }

                bool shouldPrevent = (wallDoor != null && !wallDoor.activeSelf) && (shopOwner != null && shopOwner.activeSelf);

                if (sodaMachine != null && moduleTop != null && sodaMachine.activeSelf && !shouldPrevent)
                {
                    if (SemiFunc.IsMultiplayer())
                    {
                        if (SemiFunc.IsMasterClient())
                        {
                            Vector3 pos = sodaMachine.transform.position;
                            Quaternion rot = Quaternion.Euler(0f, 90f, 0f);
                            GameObject shelf = PhotonNetwork.Instantiate(Plugin.CustomItemShelf.name, pos, rot);
                            ShopManagerPatch.SetParent(moduleTop.transform, shelf);
                        }

                        sodaMachine.SetActive(false);
                    }
                    else
                    {
                        Vector3 pos = sodaMachine.transform.position;
                        Quaternion rot = Quaternion.Euler(0f, 90f, 0f);
                        GameObject shelf = Object.Instantiate(Plugin.CustomItemShelf, pos, rot, moduleTop.transform);
                        sodaMachine.SetActive(false);
                    }

                    usedLocations.Add("Soda Machine");
                    shelvesSpawned++;
                    spawnedThisIteration = true;
                }
                else if (shouldPrevent && sodaMachine != null)
                {
                    sodaMachine.SetActive(true);
                }
            }

            if (spawnedThisIteration) continue;

            // This one is a bit more complex, but we're now able to always spawn two shelves.
            if (!usedLocations.Contains("cashiers shelf"))
            {
                GameObject moduleRight = GameObject.Find("Module Switch (2) right");
                GameObject cashierShelf = GameObject.Find("cashiers shelf");
                GameObject cashierDesk = GameObject.Find("cashiers desk");
                GameObject magazineHolder = GameObject.Find("Shop Magazine Holder");
                GameObject shopRegister = GameObject.Find("Shop Cash register");
                GameObject shopChair = GameObject.Find("Shop Chair");
                GameObject shopOwner = GameObject.Find("Shop Owner");

                Transform connected = moduleRight?.transform.Find("Connected");

                if (moduleRight != null && connected != null && !connected.gameObject.activeSelf && cashierShelf != null)
                {
                    // Determine if position requires special offset
                    Vector3 modulePos = moduleRight.transform.position;
                    bool useFirstOffset = Vector3.Distance(modulePos, new Vector3(-7.9544f, 0f, 7.7214f)) < 0.01f;
                    bool useSecondOffset = Vector3.Distance(modulePos, new Vector3(7.9544f, 0f, 7.2786f)) < 0.01f;

                    // Shelf spawn positions for the right side in diffrent shop Layouts
                    Vector3 shelfPos = cashierShelf.transform.position;
                    if (useFirstOffset)
                    {
                        shelfPos.x += 0.4f;
                        shelfPos.z += 1.0f;
                    }
                    else if (useSecondOffset)
                    {
                        shelfPos.x -= 0.5f;
                        shelfPos.z -= 0.9f;
                    }
                    else
                    {
                        shelfPos.x += 1.0f;
                        shelfPos.z -= 0.4f;
                    }

                    Quaternion shelfRot = cashierShelf.transform.rotation * Quaternion.Euler(0f, 180f, 0f);

                    // Shelf Spawning
                    if (SemiFunc.IsMultiplayer())
                    {
                        if (SemiFunc.IsMasterClient())
                        {
                            ShopManagerPatch.shelf = PhotonNetwork.Instantiate(Plugin.CustomItemShelf.name, shelfPos, shelfRot);
                            ShopManagerPatch.SetParent(moduleRight.transform, ShopManagerPatch.shelf);
                        }

                        cashierShelf.SetActive(false);
                        if (magazineHolder != null) magazineHolder.SetActive(false);
                        if (shopChair != null) shopChair.SetActive(false);
                        if (shopOwner != null) shopOwner.SetActive(false);
                    }
                    else
                    {
                        ShopManagerPatch.shelf = Object.Instantiate(Plugin.CustomItemShelf, shelfPos, shelfRot, moduleRight.transform);

                        cashierShelf.SetActive(false);
                        if (magazineHolder != null) magazineHolder.SetActive(false);
                        if (shopChair != null) shopChair.SetActive(false);
                        if (shopOwner != null) shopOwner.SetActive(false);
                    }

                    //Desk and Register adjustment
                    if (cashierDesk != null)
                    {
                        Vector3 deskPos = cashierDesk.transform.position;
                        if (useFirstOffset)
                        {
                            deskPos.x += 0.9f;
                            deskPos.z -= 0.0f;
                        }
                        else if (useSecondOffset)
                        {
                            deskPos.x -= 0.9f;
                            deskPos.z -= 0.0f;
                        }
                        else
                        {
                            deskPos.x += 0.0f;
                            deskPos.z -= 1.0f;
                        }
                        cashierDesk.transform.position = deskPos;
                    }

                    if (shopRegister != null)
                    {
                        Vector3 regPos = shopRegister.transform.position;
                        if (useFirstOffset)
                        {
                            regPos.x += 0.9f;
                            regPos.z -= 0.0f;
                        }
                        else if (useSecondOffset)
                        {
                            regPos.x -= 0.9f;
                            regPos.z -= 0.0f;
                        }
                        else
                        {
                            regPos.x += 0.0f;
                            regPos.z -= 1.0f;
                        }
                        shopRegister.transform.position = regPos;
                    }

                    usedLocations.Add("cashiers shelf");
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