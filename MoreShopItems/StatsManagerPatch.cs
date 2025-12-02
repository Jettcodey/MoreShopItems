using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreShopItems
{
	internal class StatsManagerPatch
	{
		// Track each item instance
		private static Dictionary<int, int> instancePrices = new Dictionary<int, int>();
		private static Dictionary<string, List<int>> itemInstanceIds = new Dictionary<string, List<int>>();

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

						// Get the price for this specific instance
						int refundAmount = 0;

						// Use the first tracked instance of this item
						if (itemInstanceIds.ContainsKey(itemName) && itemInstanceIds[itemName].Count > 0)
						{
							int instanceId = itemInstanceIds[itemName][0];
							if (instancePrices.ContainsKey(instanceId))
							{
								refundAmount = instancePrices[instanceId];

								// Clean up THIS instance only
								instancePrices.Remove(instanceId);
								itemInstanceIds[itemName].RemoveAt(0);

								if (itemInstanceIds[itemName].Count == 0)
									itemInstanceIds.Remove(itemName);

								Plugin.Logger.LogInfo($"Refunding {itemName} instance {instanceId} with price: {refundAmount}");
							}
						}

						if (refundAmount == 0)
						{
							Plugin.Logger.LogError($"NO PRICE FOUND for {itemName}! Tracking failed.");
							return false;
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

		// Track item and use the actual ItemAttributes instance Price
		[HarmonyPostfix]
		[HarmonyPatch(typeof(ShopManager), "ShoppingListItemAdd")]
		private static void Postfix_ShoppingListItemAdd(ItemAttributes item)
		{
			try
			{
				if (item != null && item.item != null)
				{
					string itemName = item.item.name;
					int instanceId = item.GetInstanceID();

					// Track Instance ID
					instancePrices[instanceId] = item.value;

					// Map item name to instance ID
					if (!itemInstanceIds.ContainsKey(itemName))
						itemInstanceIds[itemName] = new List<int>();

					itemInstanceIds[itemName].Add(instanceId);

					Plugin.Logger.LogInfo($"Added to cart: {itemName} instance {instanceId} for {item.value} (total instances: {itemInstanceIds[itemName].Count})");
				}
			}
			catch (Exception ex)
			{
				Plugin.Logger.LogError($"Error tracking cart addition: {ex}");
			}
		}

		// Remove when item removed
		[HarmonyPostfix]
		[HarmonyPatch(typeof(ShopManager), "ShoppingListItemRemove")]
		private static void Postfix_ShoppingListItemRemove(ItemAttributes item)
		{
			try
			{
				if (item != null && item.item != null)
				{
					string itemName = item.item.name;
					int instanceId = item.GetInstanceID();

					// Remove from instance tracking
					if (instancePrices.ContainsKey(instanceId))
					{
						instancePrices.Remove(instanceId);
						Plugin.Logger.LogInfo($"Removed instance {instanceId} from price tracking");
					}

					// Remove from item mapping
					if (itemInstanceIds.ContainsKey(itemName))
					{
						itemInstanceIds[itemName].Remove(instanceId);
						if (itemInstanceIds[itemName].Count == 0)
							itemInstanceIds.Remove(itemName);

						Plugin.Logger.LogInfo($"Removed from cart: {itemName} instance {instanceId}");
					}
				}
			}
			catch (Exception ex)
			{
				Plugin.Logger.LogError($"Error removing from cart: {ex}");
			}
		}

		// Clean up when purchase successful (ONLY runs if purchase allowed)
		[HarmonyPostfix]
		[HarmonyPatch(typeof(StatsManager), "ItemPurchase")]
		private static void Postfix_ItemPurchase(string itemName, ref bool __runOriginal)
		{
			try
			{
				if (!__runOriginal)
					return;

				// Remove one instance when purchased
				if (itemInstanceIds.ContainsKey(itemName) && itemInstanceIds[itemName].Count > 0)
				{
					int instanceId = itemInstanceIds[itemName][0];

					if (instancePrices.ContainsKey(instanceId))
					{
						int price = instancePrices[instanceId];
						instancePrices.Remove(instanceId);
						Plugin.Logger.LogInfo($"Successful purchase: {itemName} instance {instanceId} for {price}.");
					}

					itemInstanceIds[itemName].RemoveAt(0);

					if (itemInstanceIds[itemName].Count == 0)
						itemInstanceIds.Remove(itemName);
				}

				if (StatsManager.instance.itemDictionary.ContainsKey(itemName))
				{
					var item = StatsManager.instance.itemDictionary[itemName];
					int totalPurchased = StatsManager.instance.itemsPurchasedTotal[itemName];
					Plugin.Logger.LogInfo($"{itemName}: {totalPurchased}/{item.maxPurchaseAmount}");
				}
			}
			catch { }
		}
	}
}