using System.Collections.Generic;
using BepInEx.Configuration;
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
                default:
                    continue;
            }
            bool flag = obj.itemType == SemiFunc.itemType.item_upgrade;
            if (obj.itemType != SemiFunc.itemType.cart && obj.itemType != SemiFunc.itemType.pocket_cart && maxInShop != -2)
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
                else if ((!MoreUpgradesMOD.isLoaded() || !obj.itemAssetName.Contains("Modded")) && !(VanillaUpgradesMOD.isLoaded() & flag))
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
        ShopManagerPatch.itemConsumablesAmount_ref(__instance) = 100;
        ShopManagerPatch.itemUpgradesAmount_ref(__instance) = 50;
        ShopManagerPatch.itemHealthPacksAmount_ref(__instance) = 50;
        ShopManagerPatch.itemSpawnTargetAmount_ref(__instance) = 250;
    }

    [HarmonyPrefix]
    [HarmonyPatch("ShopInitialize")]
    private static void SpawnShelf()
    {
        if (!(RunManager.instance.levelCurrent.ResourcePath == "Shop") || !Plugin.Instance.boolConfigEntries["Spawn Additional Shelving"].Value)
            return;
        GameObject gameObject1 = GameObject.Find("Soda Shelf");
        GameObject gameObject2 = GameObject.Find("Module Switch BOT");
        if ((Object)gameObject1 == null || gameObject2.GetComponent<ModulePropSwitch>().ConnectedParent.activeSelf)
        {
            GameObject gameObject3 = GameObject.Find("Shop Magazine Stand (1)");
            GameObject gameObject4 = GameObject.Find("Shop Magazine Stand");
            GameObject gameObject5 = GameObject.Find("Module Switch (1) top");
            if ((Object)gameObject3 == null || gameObject5.GetComponent<ModulePropSwitch>().ConnectedParent.activeSelf)
            {
                GameObject gameObject6 = GameObject.Find("Module Switch (2) left");
                GameObject gameObject7 = GameObject.Find("Candy Shelf");
                if ((Object)gameObject6 == null || gameObject6.GetComponent<ModulePropSwitch>().ConnectedParent.activeSelf)
                {
                    Plugin.Logger.LogInfo((object)"Edge case found. Temporarily preventing spawn of custom shelf.");
                    return;
                }
                if (SemiFunc.IsMultiplayer())
                {
                    if (SemiFunc.IsMasterClient())
                    {
                        ShopManagerPatch.shelf = PhotonNetwork.Instantiate(Plugin.CustomItemShelf.name, gameObject6.transform.position + gameObject6.transform.right * 0.5f - gameObject6.transform.forward * 0.8f, gameObject6.transform.rotation * Quaternion.Euler(0.0f, 180f, 0.0f));
                        ShopManagerPatch.SetParent(gameObject6.transform, ShopManagerPatch.shelf);
                    }
                    else
                    {
                        if (!((Object)gameObject7 != null))
                            return;
                        gameObject7.SetActive(false);
                        return;
                    }
                }
                else
                    ShopManagerPatch.shelf = Object.Instantiate<GameObject>(Plugin.CustomItemShelf, gameObject6.transform.position + gameObject6.transform.right * 0.5f - gameObject6.transform.forward * 0.8f, gameObject6.transform.rotation * Quaternion.Euler(0.0f, 180f, 0.0f), gameObject5.transform.parent);
                if ((Object)gameObject7 != null)
                    gameObject7.SetActive(false);
            }
            else
            {
                if (SemiFunc.IsMultiplayer())
                {
                    if (SemiFunc.IsMasterClient())
                    {
                        ShopManagerPatch.shelf = PhotonNetwork.Instantiate(Plugin.CustomItemShelf.name, gameObject3.transform.position, gameObject3.transform.rotation * Quaternion.Euler(0.0f, 90f, 0.0f));
                        ShopManagerPatch.SetParent(gameObject5.transform, ShopManagerPatch.shelf);
                    }
                    else
                    {
                        gameObject3.SetActive(false);
                        if (!((Object)gameObject4 != null))
                            return;
                        gameObject4.SetActive(false);
                        return;
                    }
                }
                else
                    ShopManagerPatch.shelf = Object.Instantiate<GameObject>(Plugin.CustomItemShelf, gameObject3.transform.position, gameObject3.transform.rotation * Quaternion.Euler(0.0f, 90f, 0.0f), gameObject5.transform.parent);
                gameObject3.SetActive(false);
                if ((Object)gameObject4 != null)
                    gameObject4.SetActive(false);
            }
        }
        else
        {
            if (SemiFunc.IsMultiplayer())
            {
                if (SemiFunc.IsMasterClient())
                {
                    ShopManagerPatch.shelf = PhotonNetwork.Instantiate(Plugin.CustomItemShelf.name, gameObject1.transform.position, gameObject1.transform.rotation);
                    ShopManagerPatch.SetParent(gameObject2.transform, ShopManagerPatch.shelf);
                }
                else
                {
                    gameObject1.SetActive(false);
                    return;
                }
            }
            else
                ShopManagerPatch.shelf = Object.Instantiate<GameObject>(Plugin.CustomItemShelf, gameObject1.transform.position, gameObject1.transform.rotation, gameObject2.transform);
            gameObject1.SetActive(false);
        }
        Plugin.Logger.LogInfo((object)"Successfully spawned the shelf!");
    }
}