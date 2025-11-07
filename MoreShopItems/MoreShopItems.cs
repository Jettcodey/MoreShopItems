using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using MoreShopItems.Config;

namespace MoreShopItems
{
	[BepInPlugin("Jettcodey.MoreShopItems", "More Shop Items", "3.0.2")]
	[BepInDependency("bulletbot.moreupgrades", BepInDependency.DependencyFlags.SoftDependency)]
	public class Plugin : BaseUnityPlugin
	{
		internal static Plugin? Instance { get; private set; }
		internal static new ManualLogSource? Logger { get; private set; }
		internal static GameObject? CustomItemShelf;

		private readonly Harmony _harmony = new Harmony("MoreShopItems");

		internal Dictionary<string, ConfigEntry<int>> intConfigEntries = new Dictionary<string, ConfigEntry<int>>();
		internal Dictionary<string, ConfigEntry<bool>> boolConfigEntries = new Dictionary<string, ConfigEntry<bool>>();

		private void Awake()
		{
			Instance = this;
			Logger = base.Logger;
			LoadConfig();

			AssetBundle bundle = LoadAssetBundle("moreshopitems_assets.file");
			CustomItemShelf = LoadAssetFromBundle(bundle, "custom_soda_shelf");

			if (CustomItemShelf == null)
			{
				Logger.LogError("Failed to load CustomItemShelf from asset bundle.");
				return;
			}

			// Harmony patches
			_harmony.PatchAll(typeof(ShopManagerPatch));
			_harmony.PatchAll(typeof(PunManagerPatch));
			Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_NAME} v{MyPluginInfo.PLUGIN_VERSION} loaded successfully.");
		}

		private AssetBundle LoadAssetBundle(string filename)
		{
			string pluginDir = Path.GetDirectoryName(Instance?.Info.Location) ?? "";
			string fullPath = Path.Combine(pluginDir, filename);
			if (!File.Exists(fullPath))
			{
				Logger.LogError($"Asset bundle not found at {fullPath}");
				return null;
			}

			AssetBundle bundle = AssetBundle.LoadFromFile(fullPath);
			if (bundle == null)
				Logger.LogError($"Failed to load asset bundle from {fullPath}");
			return bundle;
		}

		private GameObject LoadAssetFromBundle(AssetBundle bundle, string assetName)
		{
			if (bundle == null) return null!;
			GameObject asset = bundle.LoadAsset<GameObject>(assetName);
			if (asset == null)
				Logger.LogError($"Asset '{assetName}' not found in bundle.");
			return asset;
		}

		private void LoadConfig()
		{
			string[] configDescriptions = ConfigEntries.GetConfigDescriptions();

			// Integer config entries
			intConfigEntries.Add("Max Upgrades In Shop", ConfigHelper.CreateConfig("Upgrades", "Max Upgrades In Shop", 5, configDescriptions[0], -1, 20));
			intConfigEntries.Add("Max Upgrade Purchase Amount", ConfigHelper.CreateConfig("Upgrades", "Max Upgrade Purchase Amount", 0, configDescriptions[1], 0, 1000));
			intConfigEntries.Add("Max Melee Weapons In Shop", ConfigHelper.CreateConfig("Weapons", "Max Melee Weapons In Shop", 5, configDescriptions[2], -1, 20));
			intConfigEntries.Add("Max Melee Weapon Purchase Amount", ConfigHelper.CreateConfig("Weapons", "Max Melee Weapon Purchase Amount", 0, configDescriptions[3], 0, 1000));
			intConfigEntries.Add("Max Guns In Shop", ConfigHelper.CreateConfig("Weapons", "Max Guns In Shop", 5, configDescriptions[4], -1, 20));
			intConfigEntries.Add("Max Gun Purchase Amount", ConfigHelper.CreateConfig("Weapons", "Max Gun Purchase Amount", 0, configDescriptions[5], 0, 1000));
			intConfigEntries.Add("Max Grenades In Shop", ConfigHelper.CreateConfig("Weapons", "Max Grenades In Shop", 5, configDescriptions[6], -1, 20));
			intConfigEntries.Add("Max Grenade Purchase Amount", ConfigHelper.CreateConfig("Weapons", "Max Grenade Purchase Amount", 0, configDescriptions[7], 0, 1000));
			intConfigEntries.Add("Max Mines In Shop", ConfigHelper.CreateConfig("Weapons", "Max Mines In Shop", 5, configDescriptions[8], -1, 20));
			intConfigEntries.Add("Max Mine Purchase Amount", ConfigHelper.CreateConfig("Weapons", "Max Mine Purchase Amount", 0, configDescriptions[9], 0, 1000));
			intConfigEntries.Add("Max Health-Packs In Shop", ConfigHelper.CreateConfig("Health-Packs", "Max Health-Packs In Shop", 15, configDescriptions[10], -1, 20));
			intConfigEntries.Add("Max Health-Pack Purchase Amount", ConfigHelper.CreateConfig("Health-Packs", "Max Health-Pack Purchase Amount", 0, configDescriptions[11], 0, 1000));
			intConfigEntries.Add("Max Drones In Shop", ConfigHelper.CreateConfig("Utilities", "Max Drones In Shop", 5, configDescriptions[12], -1, 20));
			intConfigEntries.Add("Max Drone Purchase Amount", ConfigHelper.CreateConfig("Utilities", "Max Drone Purchase Amount", 0, configDescriptions[13], 0, 1000));
			intConfigEntries.Add("Max Orbs In Shop", ConfigHelper.CreateConfig("Utilities", "Max Orbs In Shop", 5, configDescriptions[14], -1, 20));
			intConfigEntries.Add("Max Orb Purchase Amount", ConfigHelper.CreateConfig("Utilities", "Max Orb Purchase Amount", 0, configDescriptions[15], 0, 1000));
			intConfigEntries.Add("Max Crystals In Shop", ConfigHelper.CreateConfig("Utilities", "Max Crystals In Shop", 10, configDescriptions[16], -1, 20));
			intConfigEntries.Add("Max Crystal Purchase Amount", ConfigHelper.CreateConfig("Utilities", "Max Crystal Purchase Amount", 0, configDescriptions[17], 0, 1000));
			intConfigEntries.Add("Max Trackers In Shop", ConfigHelper.CreateConfig("Utilities", "Max Trackers In Shop", 5, configDescriptions[18], -1, 20));
			intConfigEntries.Add("Max Tracker Purchase Amount", ConfigHelper.CreateConfig("Utilities", "Max Tracker Purchase Amount", 0, configDescriptions[19], 0, 1000));
			intConfigEntries.Add("Max Carts In Shop", ConfigHelper.CreateConfig("Carts", "Max Carts In Shop", 2, configDescriptions[24], -1, 4));
			intConfigEntries.Add("Max Cart Purchase Amount", ConfigHelper.CreateConfig("Carts", "Max Cart Purchase Amount", 0, configDescriptions[25], 0, 100));
			intConfigEntries.Add("Max Pocket Carts In Shop", ConfigHelper.CreateConfig("Carts", "Max Pocket Carts In Shop", 2, configDescriptions[26], -1, 4));
			intConfigEntries.Add("Max Pocket Cart Purchase Amount", ConfigHelper.CreateConfig("Carts", "Max Pocket Cart Purchase Amount", 0, configDescriptions[27], 0, 100));
			intConfigEntries.Add("Max Tools In Shop", ConfigHelper.CreateConfig("Tools", "Max Tools In Shop", 2, configDescriptions[28], -1, 10));
			intConfigEntries.Add("Max Tool Purchase Amount", ConfigHelper.CreateConfig("Tools", "Max Tool Purchase Amount", 0, configDescriptions[29], 0, 100));
			intConfigEntries.Add("Max Additional Shelves In Shop", ConfigHelper.CreateConfig("General", "Max Additional Shelves In Shop", 2, configDescriptions[23], 0, 2));

			// Boolean config entries
			boolConfigEntries.Add("Override Modded Items", ConfigHelper.CreateConfig("General", "Override Modded Items", true, configDescriptions[20], -1, -1));
			boolConfigEntries.Add("Override Single-Use Upgrades", ConfigHelper.CreateConfig("General", "Override Single-Use Upgrades", false, configDescriptions[21], -1, -1));
			boolConfigEntries.Add("Spawn Additional Shelving", ConfigHelper.CreateConfig("General", "Spawn Additional Shelving", true, configDescriptions[22], -1, -1));
			boolConfigEntries.Add("Log Potential Items", ConfigHelper.CreateConfig("Dev General", "Log Potential Items", false, configDescriptions[30], -1, -1));
		}
	}
}
