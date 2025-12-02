using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using MoreShopItems.Compatability;
using Photon.Pun;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using sys = System;

namespace MoreShopItems
{
	[HarmonyPatch(typeof(ShopManager))]
	internal static class ShopManagerPatch
	{
		private static readonly AccessTools.FieldRef<ShopManager, int> itemSpawnTargetAmount_ref = AccessTools.FieldRefAccess<ShopManager, int>("itemSpawnTargetAmount");
		private static readonly AccessTools.FieldRef<ShopManager, int> itemConsumablesAmount_ref = AccessTools.FieldRefAccess<ShopManager, int>("itemConsumablesAmount");
		private static readonly AccessTools.FieldRef<ShopManager, int> itemUpgradesAmount_ref = AccessTools.FieldRefAccess<ShopManager, int>("itemUpgradesAmount");
		private static readonly AccessTools.FieldRef<ShopManager, int> itemHealthPacksAmount_ref = AccessTools.FieldRefAccess<ShopManager, int>("itemHealthPacksAmount");
		private static GameObject? shelf;
		internal static bool isMoreUpgrades = false;

		// Price fix patches for 6+ players
		// UpgradeValueGet patch
		[HarmonyPatch(typeof(ShopManager), "UpgradeValueGet")]
		public static class ShopManager_UpgradeValueGet_Patch
		{
			[HarmonyPrefix]
			public static bool Prefix(ref float __result, float _value, Item item)
			{
				var instance = ShopManager.instance;
				if (instance == null)
					return true; // fallback to original

				int playerCount = Mathf.Max(1, GameDirector.instance.PlayerList.Count);
				float playerReductionFactor = 0.1f * (playerCount - 1);
				float cappedReduction = Mathf.Min(0.5f, playerReductionFactor);

				float v = _value;
				v -= v * cappedReduction;

				if (item != null)
					v += v * instance.upgradeValueIncrease * (float)StatsManager.instance.GetItemsUpgradesPurchased(item.name);

				v = Mathf.Ceil(v);
				__result = Mathf.Max(v, 1f);
				return false;
			}
		}

		// HealthPackValueGet patch
		[HarmonyPatch(typeof(ShopManager), "HealthPackValueGet")]
		public static class ShopManager_HealthPackValueGet_Patch
		{
			[HarmonyPrefix]
			public static bool Prefix(ref float __result, float _value)
			{
				var instance = ShopManager.instance;
				if (instance == null)
					return true;

				int playerCount = Mathf.Max(1, GameDirector.instance.PlayerList.Count);
				float playerReductionFactor = 0.1f * (playerCount - 1);
				float cappedReduction = Mathf.Min(0.5f, playerReductionFactor);

				float v = _value;
				int levelsCompleted = Mathf.Min(RunManager.instance.levelsCompleted, 15);

				v -= v * cappedReduction;
				v += v * instance.healthPackValueIncrease * (float)levelsCompleted;

				v = Mathf.Ceil(v);
				__result = Mathf.Max(v, 1f);
				return false;
			}
		}

		// ItemAttributes.GetValue patch
		[HarmonyPatch(typeof(ItemAttributes), "GetValue")]
		public static class ItemAttributes_GetValue_Patch
		{
			[HarmonyPrefix]
			public static bool Prefix(ItemAttributes __instance)
			{
				if (GameManager.Multiplayer() && !PhotonNetwork.IsMasterClient)
					return true; // let original run on non-master clients

				// get private fields via reflection
				var t = typeof(ItemAttributes);
				var fMin = AccessTools.Field(t, "itemValueMin");
				var fMax = AccessTools.Field(t, "itemValueMax");
				if (fMin == null || fMax == null)
					return true; // fallback

				float itemValueMin = (float)fMin.GetValue(__instance);
				float itemValueMax = (float)fMax.GetValue(__instance);

				float baseValue = UnityEngine.Random.Range(itemValueMin, itemValueMax) * ShopManager.instance.itemValueMultiplier;
				if (baseValue < 1000f) baseValue = 1000f;

				float finalValue = Mathf.Ceil(baseValue / 1000f);

				if (__instance.itemType == SemiFunc.itemType.item_upgrade)
					finalValue = ShopManager.instance.UpgradeValueGet(finalValue, __instance.item);
				else if (__instance.itemType == SemiFunc.itemType.healthPack)
					finalValue = ShopManager.instance.HealthPackValueGet(finalValue);
				else if (__instance.itemType == SemiFunc.itemType.power_crystal)
					finalValue = ShopManager.instance.CrystalValueGet(finalValue);

				finalValue = Mathf.Max(finalValue, 1f);

				int intFinal = (int)finalValue;

				// set internal 'value' field via reflection (field name is "value")
				var fValue = AccessTools.Field(t, "value");
				if (fValue != null)
					fValue.SetValue(__instance, intFinal);
				else
					__instance.value = intFinal; // try direct set if accessible

				if (GameManager.Multiplayer())
					__instance.photonView.RPC("GetValueRPC", RpcTarget.Others, (object)intFinal);

				return false;
			}
		}

		private static void SetItemValues(Item item, int maxInShop, int maxPurchaseAmount)
		{
			item.maxAmountInShop = maxInShop;
			item.maxAmount = maxInShop;

			item.maxPurchase = maxPurchaseAmount > 0;
			item.maxPurchaseAmount = maxPurchaseAmount;

			if (Plugin.Instance.boolConfigEntries["Item Spawn Logs"].Value)
			{
				Plugin.Logger.LogInfo($"Set values for {item.name}: maxInShop={maxInShop}, maxPurchase={item.maxPurchase}, maxPurchaseAmount={maxPurchaseAmount}");
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch("ShopInitialize")]
		private static void AdjustItems()
		{
			if (!(RunManager.instance.levelCurrent.ResourcePath == "Shop") || !(StatsManager.instance != null) || !SemiFunc.IsMasterClient() && SemiFunc.IsMultiplayer())
				return;

			Dictionary<string, ConfigEntry<int>> intConfigEntries = Plugin.Instance.intConfigEntries;
			Dictionary<string, ConfigEntry<bool>> boolConfigEntries = Plugin.Instance.boolConfigEntries;

			Plugin.Logger.LogInfo("Override modded items = " + boolConfigEntries["Override Modded Items"].Value.ToString());

			foreach (Item obj in StatsManager.instance.itemDictionary.Values)
			{
				/*// Skip if we're using vanilla spawn amounts
				if (boolConfigEntries["Use Game Default Spawn Amounts"].Value)
				{
					continue;
				}*/

				int maxInShop = -1;
				int maxPurchaseAmount = 0;
				bool shouldOverride = false;

				switch (obj.itemType)
				{
					case SemiFunc.itemType.drone:
						maxInShop = intConfigEntries["Max Drones In Shop"].Value;
						maxPurchaseAmount = intConfigEntries["Max Drone Purchase Amount"].Value;
						shouldOverride = maxInShop != -1;
						break;
					case SemiFunc.itemType.orb:
						maxInShop = intConfigEntries["Max Orbs In Shop"].Value;
						maxPurchaseAmount = intConfigEntries["Max Orb Purchase Amount"].Value;
						shouldOverride = maxInShop != -1;
						break;
					case SemiFunc.itemType.item_upgrade:
						maxInShop = intConfigEntries["Max Upgrades In Shop"].Value;
						maxPurchaseAmount = intConfigEntries["Max Upgrade Purchase Amount"].Value;
						shouldOverride = maxInShop != -1;
						break;
					case SemiFunc.itemType.power_crystal:
						maxInShop = intConfigEntries["Max Crystals In Shop"].Value + 1;
						maxPurchaseAmount = intConfigEntries["Max Crystal Purchase Amount"].Value;
						shouldOverride = maxInShop != -1;
						break;
					case SemiFunc.itemType.grenade:
						maxInShop = intConfigEntries["Max Grenades In Shop"].Value;
						maxPurchaseAmount = intConfigEntries["Max Grenade Purchase Amount"].Value;
						shouldOverride = maxInShop != -1;
						break;
					case SemiFunc.itemType.melee:
						maxInShop = intConfigEntries["Max Melee Weapons In Shop"].Value;
						maxPurchaseAmount = intConfigEntries["Max Melee Weapon Purchase Amount"].Value;
						shouldOverride = maxInShop != -1;
						break;
					case SemiFunc.itemType.healthPack:
						maxInShop = intConfigEntries["Max Health-Packs In Shop"].Value;
						maxPurchaseAmount = intConfigEntries["Max Health-Pack Purchase Amount"].Value;
						shouldOverride = maxInShop != -1;
						break;
					case SemiFunc.itemType.gun:
						maxInShop = intConfigEntries["Max Guns In Shop"].Value;
						maxPurchaseAmount = intConfigEntries["Max Gun Purchase Amount"].Value;
						shouldOverride = maxInShop != -1;
						break;
					case SemiFunc.itemType.tracker:
						maxInShop = intConfigEntries["Max Trackers In Shop"].Value;
						maxPurchaseAmount = intConfigEntries["Max Tracker Purchase Amount"].Value;
						shouldOverride = maxInShop != -1;
						break;
					case SemiFunc.itemType.mine:
						maxInShop = intConfigEntries["Max Mines In Shop"].Value;
						maxPurchaseAmount = intConfigEntries["Max Mine Purchase Amount"].Value;
						shouldOverride = maxInShop != -1;
						break;
					case SemiFunc.itemType.cart:
						maxInShop = intConfigEntries["Max Carts In Shop"].Value;
						maxPurchaseAmount = intConfigEntries["Max Cart Purchase Amount"].Value;
						shouldOverride = maxInShop != -1;
						break;
					case SemiFunc.itemType.pocket_cart:
						maxInShop = intConfigEntries["Max Pocket Carts In Shop"].Value;
						maxPurchaseAmount = intConfigEntries["Max Pocket Cart Purchase Amount"].Value;
						shouldOverride = maxInShop != -1;
						break;
					case SemiFunc.itemType.tool:
						maxInShop = intConfigEntries["Max Tools In Shop"].Value;
						maxPurchaseAmount = intConfigEntries["Max Tool Purchase Amount"].Value;
						shouldOverride = maxInShop != -1;
						break;
					default:
						continue;
				}

				if (shouldOverride)
				{
					bool isUpgrade = obj.itemType == SemiFunc.itemType.item_upgrade;

					if (boolConfigEntries["Override Modded Items"].Value)
					{
						if (isUpgrade && boolConfigEntries["Override Single-Use Upgrades"].Value)
						{
							ShopManagerPatch.SetItemValues(obj, maxInShop, maxPurchaseAmount);
						}
						else if (isUpgrade && !obj.maxPurchase)
						{
							ShopManagerPatch.SetItemValues(obj, maxInShop, maxPurchaseAmount);
						}
						else if (!isUpgrade)
						{
							ShopManagerPatch.SetItemValues(obj, maxInShop, maxPurchaseAmount);
						}
					}
					else
					{
						// Only override non-modded items
						bool isModdedItem = (MoreUpgradesMOD.isLoaded() || NikkisUpgradesMOD.isLoaded()) && obj.name.Contains("Modded");

						if (!isModdedItem && !(VanillaUpgradesMOD.isLoaded() && isUpgrade))
						{
							if (isUpgrade && boolConfigEntries["Override Single-Use Upgrades"].Value)
							{
								ShopManagerPatch.SetItemValues(obj, maxInShop, maxPurchaseAmount);
							}
							else if (isUpgrade && !obj.maxPurchase)
							{
								ShopManagerPatch.SetItemValues(obj, maxInShop, maxPurchaseAmount);
							}
							else if (!isUpgrade)
							{
								ShopManagerPatch.SetItemValues(obj, maxInShop, maxPurchaseAmount);
							}
						}
					}
				}
			}
		}

		[HarmonyPrefix]
		[HarmonyPatch(typeof(ShopManager), "GetAllItemsFromStatsManager")]
		private static void Prefix_GetAllItemsFromStatsManager()
		{
			// Run AdjustItems again to ensure all item values are set before the shop populates
			AdjustItems();
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
			itemConsumablesAmount_ref(__instance) = 100;
			itemUpgradesAmount_ref(__instance) = 180;
			itemHealthPacksAmount_ref(__instance) = 60;
			itemSpawnTargetAmount_ref(__instance) = 450;
		}

		[HarmonyPostfix]
		[HarmonyPatch("GetAllItemsFromStatsManager")]
		static void Postfix(ShopManager __instance)
		{
			__instance.itemConsumablesAmount = 100;
			Plugin.Logger.LogInfo($"Forced itemConsumablesAmount to {__instance.itemConsumablesAmount}");
		}

		[HarmonyPrefix]
		[HarmonyPatch("ShopInitialize")]
		private static void SpawnShelf()
		{
			if (!(RunManager.instance.levelCurrent.ResourcePath == "Shop") || !Plugin.Instance.boolConfigEntries["Spawn Additional Shelving"].Value)
				return;

			ShelfEventListener.Ensure();

			int maxShelvesToSpawn = Plugin.Instance.intConfigEntries["Max Additional Shelves In Shop"].Value;
			if (maxShelvesToSpawn <= 0)
				return;

			HashSet<string> usedLocations = new HashSet<string>();
			int shelvesSpawned = 0;

			for (int i = 0; i < maxShelvesToSpawn; i++)
			{
				bool spawnedThisIteration = false;

				// --- Soda Shelf ---
				if (!usedLocations.Contains("Soda"))
				{
					GameObject sodaShelf = GameObject.Find("Soda Shelf");
					GameObject moduleBottom = GameObject.Find("Module Switch BOT");
					if (sodaShelf != null && moduleBottom != null && !moduleBottom.GetComponent<ModulePropSwitch>().ConnectedParent.activeSelf)
					{
						Vector3 pos = sodaShelf.transform.position;
						Quaternion rot = sodaShelf.transform.rotation;
						Transform parent = moduleBottom.transform;
						string placeholder = "Soda Shelf";

						if (SemiFunc.IsMultiplayer() && SemiFunc.IsMasterClient())
						{
							GameObject spawned = ShelfSpawner.Spawn(pos, rot, parent, placeholder);
							if (spawned != null) SetParent(parent, spawned);
						}
						else if (!SemiFunc.IsMultiplayer())
						{
							Object.Instantiate(Plugin.CustomItemShelf, pos, rot, parent);
						}

						sodaShelf.SetActive(false);
						usedLocations.Add("Soda");
						shelvesSpawned++;
						spawnedThisIteration = true;
					}
				}

				if (spawnedThisIteration) continue;

				// --- Magazine Shelf ---
				if (!usedLocations.Contains("Magazine"))
				{
					GameObject moduleTop = GameObject.Find("Module Switch (1) top");
					GameObject magazineStand_1 = GameObject.Find("Shop Magazine Stand (1)");
					GameObject magazineStand = GameObject.Find("Shop Magazine Stand");

					if (magazineStand_1 != null && moduleTop != null && !moduleTop.GetComponent<ModulePropSwitch>().ConnectedParent.activeSelf)
					{
						Vector3 pos = magazineStand_1.transform.position;
						Quaternion rot = magazineStand_1.transform.rotation * Quaternion.Euler(0.0f, 90f, 0.0f);
						Transform parent = moduleTop.transform.parent;
						string placeholder = "Shop Magazine Stand (1)";

						if (SemiFunc.IsMultiplayer() && SemiFunc.IsMasterClient())
						{
							GameObject spawned = ShelfSpawner.Spawn(pos, rot, parent, placeholder);
							if (spawned != null) SetParent(parent, spawned);
						}
						else if (!SemiFunc.IsMultiplayer())
						{
							Object.Instantiate(Plugin.CustomItemShelf, pos, rot, parent);
						}

						magazineStand_1.SetActive(false);
						if (magazineStand != null) magazineStand.SetActive(false);

						usedLocations.Add("Magazine");
						shelvesSpawned++;
						spawnedThisIteration = true;
					}
				}

				if (spawnedThisIteration) continue;

				// --- Candy Shelf ---
				if (!usedLocations.Contains("Candy"))
				{
					GameObject moduleLeft = GameObject.Find("Module Switch (2) left");
					GameObject candyShelf = GameObject.Find("Candy Shelf");

					if (moduleLeft != null && candyShelf != null && !moduleLeft.GetComponent<ModulePropSwitch>().ConnectedParent.activeSelf)
					{
						Vector3 pos = moduleLeft.transform.position + moduleLeft.transform.right * 0.5f - moduleLeft.transform.forward * 0.8f;
						Quaternion rot = moduleLeft.transform.rotation * Quaternion.Euler(0.0f, 180f, 0.0f);
						Transform parent = moduleLeft.transform;
						string placeholder = "Candy Shelf";

						if (SemiFunc.IsMultiplayer() && SemiFunc.IsMasterClient())
						{
							GameObject spawned = ShelfSpawner.Spawn(pos, rot, parent, placeholder);
							if (spawned != null) SetParent(parent, spawned);
						}
						else if (!SemiFunc.IsMultiplayer())
						{
							Object.Instantiate(Plugin.CustomItemShelf, pos, rot, parent);
						}

						candyShelf.SetActive(false);
						usedLocations.Add("Candy");
						shelvesSpawned++;
						spawnedThisIteration = true;
					}
				}

				if (spawnedThisIteration) continue;

				// --- Soda Machine Shelf ---
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
						Vector3 pos = sodaMachine.transform.position;
						Quaternion rot = Quaternion.Euler(0f, 90f, 0f);
						Transform parent = moduleTop.transform;
						string placeholder = "Soda Machine (1)";

						if (SemiFunc.IsMultiplayer() && SemiFunc.IsMasterClient())
						{
							GameObject spawned = ShelfSpawner.Spawn(pos, rot, parent, placeholder);
							if (spawned != null) SetParent(parent, spawned);
						}
						else if (!SemiFunc.IsMultiplayer())
						{
							Object.Instantiate(Plugin.CustomItemShelf, pos, rot, parent);
						}

						sodaMachine.SetActive(false);
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

				// --- Cashier Shelf ---
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
						Vector3 modulePos = moduleRight.transform.position;
						bool useFirstOffset = Vector3.Distance(modulePos, new Vector3(-7.9544f, 0f, 7.7214f)) < 0.01f;
						bool useSecondOffset = Vector3.Distance(modulePos, new Vector3(7.9544f, 0f, 7.2786f)) < 0.01f;

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
						Transform parent = moduleRight.transform;
						string placeholder = "cashiers shelf";

						if (SemiFunc.IsMultiplayer() && SemiFunc.IsMasterClient())
						{
							GameObject spawned = ShelfSpawner.Spawn(shelfPos, shelfRot, parent, placeholder);
							if (spawned != null) SetParent(parent, spawned);
						}
						else if (!SemiFunc.IsMultiplayer())
						{
							Object.Instantiate(Plugin.CustomItemShelf, shelfPos, shelfRot, parent);
						}

						cashierShelf.SetActive(false);
						if (magazineHolder != null) magazineHolder.SetActive(false);
						if (shopChair != null) shopChair.SetActive(false);
						if (shopOwner != null) shopOwner.SetActive(false);

						if (cashierDesk != null)
						{
							Vector3 deskPos = cashierDesk.transform.position;
							if (useFirstOffset) deskPos.x += 0.9f;
							else if (useSecondOffset) deskPos.x -= 0.9f;
							else deskPos.z -= 1.0f;
							cashierDesk.transform.position = deskPos;
						}

						if (shopRegister != null)
						{
							Vector3 regPos = shopRegister.transform.position;
							if (useFirstOffset) regPos.x += 0.9f;
							else if (useSecondOffset) regPos.x -= 0.9f;
							else regPos.z -= 1.0f;
							shopRegister.transform.position = regPos;
						}

						usedLocations.Add("cashiers shelf");
						shelvesSpawned++;
						spawnedThisIteration = true;
					}
				}

				if (!spawnedThisIteration)
				{
					Plugin.Logger.LogInfo($"No more available locations found. Stopping shelf spawn at {shelvesSpawned} shelves.");
					break;
				}
			}

			if (shelvesSpawned > 0)
			{
				Plugin.Logger.LogInfo($"Successfully spawned {shelvesSpawned} custom shelf/shelves!");
			}
		}

		[HarmonyPostfix]
		[HarmonyPatch("GetAllItemVolumesInScene")]
		private static void LogPotentialItems(ShopManager __instance)
		{
			if (Plugin.Instance.boolConfigEntries["Item Spawn Logs"].Value)
			{
				if (__instance == null) return;

				Plugin.Logger.LogInfo("--- Shop Initialization: Starting Item Scan ---");
				Task.Delay(2000).Wait(); // Wait for a moment to ensure items are loaded !IMPORTANT!

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
						maxNameLength = item.itemName.Length;
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

		internal static class ShelfSpawner
		{
			public static GameObject? Spawn(Vector3 pos, Quaternion rot, Transform parentTransform, string placeholderName)
			{
				try
				{
					if (!SemiFunc.IsMultiplayer())
					{
						if (Plugin.CustomItemShelf == null)
						{
							Plugin.Logger.LogError("CustomItemShelf is null. Cannot spawn shelf.");
							return null;
						}

						if (parentTransform != null)
							return Object.Instantiate(Plugin.CustomItemShelf, pos, rot, parentTransform);
						return Object.Instantiate(Plugin.CustomItemShelf, pos, rot);
					}

					if (SemiFunc.IsMasterClient())
					{
						if (Plugin.CustomItemShelf == null)
						{
							Plugin.Logger.LogError("CustomItemShelf is null on host. Cannot spawn shelf.");
							return null;
						}

						string id = sys.Guid.NewGuid().ToString("N");
						GameObject local = Object.Instantiate(Plugin.CustomItemShelf, pos, rot);
						local.name = "MoreShopShelf_" + id;

						if (parentTransform != null)
							local.transform.SetParent(parentTransform, worldPositionStays: true);

						if (!string.IsNullOrEmpty(placeholderName))
						{
							GameObject placeholder = GameObject.Find(placeholderName);
							if (placeholder != null)
								placeholder.SetActive(false);
						}

						string parentName = parentTransform != null ? parentTransform.gameObject.name : string.Empty;

						try
						{
							ShelfEvents.RaiseSpawnShelf(id, pos, rot, parentName, placeholderName ?? string.Empty);
						}
						catch (sys.Exception ex)
						{
							Plugin.Logger.LogError("ShelfSpawner: RaiseSpawnShelf failed: " + ex);
						}

						return local;
					}

					return null;
				}
				catch (sys.Exception ex)
				{
					Plugin.Logger.LogError("ShelfSpawner.Spawn exception: " + ex);
					return null;
				}
			}
		}
	}
}
