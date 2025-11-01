using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace MoreShopItems
{
	internal static class ShelfEvents
	{
		public const byte EV_SPAWN_SHELF = 155; // Please dont conflict with other mods

		public static void RaiseSpawnShelf(string shelfID, Vector3 pos, Quaternion rot, string parentName, string placeholder)
		{
			if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom)
			{
				Plugin.Logger.LogWarning($"[ShelfEvents] Not in room - cannot raise event for shelf {placeholder}");
				return;
			}

			object[] payload = new object[] { shelfID, pos, rot, parentName, placeholder };
			var options = new RaiseEventOptions { Receivers = ReceiverGroup.Others }; // only clients
			var send = new SendOptions { Reliability = true };
			PhotonNetwork.RaiseEvent(EV_SPAWN_SHELF, payload, options, send);

			Plugin.Logger.LogInfo($"[ShelfEvents] Master raised EV_SPAWN_SHELF for {placeholder}");
		}
	}
}
