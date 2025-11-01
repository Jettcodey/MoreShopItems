using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using UnityEngine;

namespace MoreShopItems
{
	internal class ShelfEventListener : MonoBehaviourPunCallbacks, IOnEventCallback
	{
		private static bool _created;
		private static readonly HashSet<string> spawnedShelves = new HashSet<string>();

		public static void Ensure()
		{
			if (_created) return;
			_created = true;
			var go = new GameObject("MoreShopItems_ShelfEventListener");
			Object.DontDestroyOnLoad(go);
			go.AddComponent<ShelfEventListener>();
			Plugin.Logger.LogInfo("ShelfEventListener created!");
		}

		private void OnEnable() => PhotonNetwork.AddCallbackTarget(this);
		private void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);

		public override void OnJoinedRoom()
		{
			lock (spawnedShelves) spawnedShelves.Clear();
			Plugin.Logger.LogInfo("[ShelfEventListener] OnJoinedRoom: cleared spawned shelf registry");
		}

		public override void OnLeftRoom()
		{
			lock (spawnedShelves) spawnedShelves.Clear();
			Plugin.Logger.LogInfo("[ShelfEventListener] OnLeftRoom: cleared spawned shelf registry");
		}

		public void OnEvent(EventData photonEvent)
		{
			if (photonEvent.Code != ShelfEvents.EV_SPAWN_SHELF) return;

			var data = photonEvent.CustomData as object[];
			if (data == null || data.Length < 5) return;

			string shelfID = data[0] as string;
			Vector3 pos = (Vector3)data[1];
			Quaternion rot = (Quaternion)data[2];
			string parentName = data[3] as string;
			string placeholder = data[4] as string;

			if (string.IsNullOrEmpty(shelfID)) return;
			if (spawnedShelves.Contains(shelfID)) return;

			Transform parent = GameObject.Find(parentName)?.transform;
			if (parent == null)
			{
				Plugin.Logger.LogWarning($"[ShelfEventListener] Parent {parentName} not found for shelf {placeholder}");
				return;
			}

			Object.Instantiate(Plugin.CustomItemShelf, pos, rot, parent);
			spawnedShelves.Add(shelfID);

			Plugin.Logger.LogInfo($"[ShelfEventListener] Spawned shelf {placeholder} for client via event");
		}
	}
}
