using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreShopItems
{
	internal class StatsManagerPatch
	{
		// Track pending purchases to know their price for refunds
		private static Dictionary<string, int> pendingPurchases = new Dictionary<string, int>();

		[HarmonyPrefix]
		[HarmonyPatch(typeof(StatsManager), "ItemPurchase")]
		private static bool Prefix_ItemPurchase(string itemName, ref bool __runOriginal)
		{
			__runOriginal = true;

			try
			{
				if (!StatsManager.instance.itemDictionary.ContainsKey(itemName))
					return true;

				var item = StatsManager.instance.itemDictionary[itemName];

				// Check purchase limits
				if (item.maxPurchase && item.maxPurchaseAmount > 0)
				{
					int totalPurchased = StatsManager.instance.itemsPurchasedTotal[itemName];

					if (totalPurchased >= item.maxPurchaseAmount)
					{
						// Block original purchase
						__runOriginal = false;

						// Get the price of pending purchases
						int refundAmount = 0;
						if (pendingPurchases.TryGetValue(itemName, out refundAmount))
						{
							pendingPurchases.Remove(itemName);
						}
						else
						{
							// Fallback
							refundAmount = CalculateEstimatedPrice(item);
						}

						// Refund
						if (refundAmount > 0 && StatsManager.instance.runStats.ContainsKey("currency"))
						{
							// Update currency
							int oldCurrency = StatsManager.instance.runStats["currency"];
							StatsManager.instance.runStats["currency"] += refundAmount;
							int newCurrency = StatsManager.instance.runStats["currency"];

							Plugin.Logger.LogInfo($"Refunded: {itemName} limit reached. Refunded {refundAmount} currency ({oldCurrency} -> {newCurrency})");

							// Sync currency
							if (GameManager.Multiplayer() && Photon.Pun.PhotonNetwork.IsMasterClient)
							{
								PunManager.instance.SetRunStatSet("currency", newCurrency);
							}
						}

						return false;
					}
				}
			}
			catch (Exception ex)
			{
				Plugin.Logger.LogError($"Error in purchase check: {ex}");
			}

			return true;
		}

		// Patch shop logic to capture item prices
		[HarmonyPostfix]
		[HarmonyPatch(typeof(ShopManager), "ShoppingListItemAdd")]
		private static void Postfix_ShoppingListItemAdd(ItemAttributes item)
		{
			try
			{
				if (item != null && item.item != null)
				{
					// Store the price of the item
					pendingPurchases[item.item.name] = item.value;
					Plugin.Logger.LogInfo($"Tracked purchase: {item.item.name} for {item.value}");
				}
			}
			catch (Exception ex)
			{
				Plugin.Logger.LogError($"Error tracking purchase: {ex}");
			}
		}

		// Clean up when removed (not being purchased)
		[HarmonyPostfix]
		[HarmonyPatch(typeof(ShopManager), "ShoppingListItemRemove")]
		private static void Postfix_ShoppingListItemRemove(ItemAttributes item)
		{
			try
			{
				if (item != null && item.item != null)
				{
					pendingPurchases.Remove(item.item.name);
					Plugin.Logger.LogInfo($"Removed tracked purchase: {item.item.name} (not being purchased)");
				}
			}
			catch (Exception ex)
			{
				Plugin.Logger.LogError($"Error removing tracked purchase: {ex}");
			}
		}

		// Clean up after purchase
		[HarmonyPostfix]
		[HarmonyPatch(typeof(StatsManager), "ItemPurchase")]
		private static void Postfix_ItemPurchase(string itemName)
		{
			try
			{
				pendingPurchases.Remove(itemName);

				if (StatsManager.instance.itemDictionary.ContainsKey(itemName))
				{
					var item = StatsManager.instance.itemDictionary[itemName];
					int totalPurchased = StatsManager.instance.itemsPurchasedTotal[itemName];
					Plugin.Logger.LogInfo($"{itemName}: {totalPurchased}/{item.maxPurchaseAmount}");
				}
			}
			catch { }
		}

		private static int CalculateEstimatedPrice(Item item)
		{
			try
			{
				// Find an existing instance of this item in the shop
				ItemAttributes[] allItems = GameObject.FindObjectsOfType<ItemAttributes>();
				foreach (var itemAttr in allItems)
				{
					if (itemAttr.item != null && itemAttr.item.name == item.name)
					{
						return itemAttr.value;
					}
				}

				// If no instance found wee calculate based on more general item value
				int baseValue = (int)((item.value.valueMin + item.value.valueMax) / 2000f * ShopManager.instance.itemValueMultiplier);
				return Mathf.Max(1, baseValue);
			}
			catch
			{
				return 100;
			}
		}
	}
}