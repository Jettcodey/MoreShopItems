using HarmonyLib;
using Photon.Pun;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreShopItems
{
	[HarmonyPatch(typeof(PunManager))]
	internal static class PunManagerPatch
	{
		[HarmonyPrefix]
		[HarmonyPatch("TruckPopulateItemVolumes")]
		private static void RemoveNullValues()
		{
			if (!((UnityEngine.Object)ItemManager.instance != null) || !SemiFunc.IsMasterClient() && SemiFunc.IsMultiplayer())
				return;
			Predicate<ItemVolume> match = (Predicate<ItemVolume>)(volume => (UnityEngine.Object)volume == null);
			ItemManager.instance.itemVolumes.RemoveAll(match);
		}

		[HarmonyPrefix]
		[HarmonyPatch("ShopUpdateCost")]
		public static void ShopUpdateCost_Postfix(PunManager __instance)
		{
			if (!SemiFunc.IsMasterClientOrSingleplayer() || !SemiFunc.IsMultiplayer())
				return;

			int playerCount = Mathf.Max(1, PhotonNetwork.PlayerList.Length);
			Plugin.Logger.LogInfo($"Player count for cost update: {playerCount}");

			foreach (var item in ShopManager.instance.shoppingList)
			{
				if (item == null) continue;

				int baseValue = item.value;
				float scale;

				if (playerCount <= 6)
					scale = 1f;
				else
					scale = 1f + 0.15f * (playerCount - 6);
				
				Plugin.Logger.LogInfo($"Scaling factor for item '{item.name}': {scale}");


				item.value = Mathf.Max(1, Mathf.RoundToInt(baseValue * scale));

				Plugin.Logger.LogInfo($"Updated value for item '{item.name}': {item.value}");
			}

			int totalCost = 0;
			foreach (var shopping in ShopManager.instance.shoppingList)
			{
				if (shopping != null)
					totalCost += shopping.value;
			}

			Plugin.Logger.LogInfo($"Total shopping cost after scaling: {totalCost}");

			__instance.photonView.RPC("UpdateShoppingCostRPC", RpcTarget.All, totalCost);
			Plugin.Logger.LogInfo($"RPC 'UpdateShoppingCostRPC' called with value: {totalCost}");
		}

		[HarmonyPrefix]
		[HarmonyPatch("UpdateShoppingCostRPC")]
		public static void UpdateShoppingCostRPC_Prefix(ref int value)
		{
			int playerCount = Mathf.Max(1, PhotonNetwork.PlayerList.Length);
			Plugin.Logger.LogInfo($"Player count for RPC scaling: {playerCount}");

			float scale;
			if (playerCount <= 6)
				scale = 1f;
			else
				scale = 1f + 0.15f * (playerCount - 6);

			Plugin.Logger.LogInfo($"RPC scaling factor: {scale}");

			value = Mathf.Max(1, Mathf.RoundToInt(value * scale));
			Plugin.Logger.LogInfo($"Scaled RPC value: {value}");
		}
	}
}