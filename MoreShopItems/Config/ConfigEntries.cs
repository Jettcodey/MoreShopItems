using System;
using System.Text;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Photon.Pun;
using REPOLib;
using UnityEngine;

namespace MoreShopItems.Config
{
    public static class ConfigEntries
    {
        private static string[] DESCRIPTIONS = new string[25]
        {
            "How many of each upgrade to spawn in the shop.",
            "How many upgrades you can purchase total. Set 0 to disable",
            "How many of each melee weapon to spawn in the shop.",
            "How many melee weapons you can purchase total. Set 0 to disable",
            "How many of each gun to spawn in the shop.",
            "How many guns you can purchase total. Set 0 to disable",
            "How many of each grenade to spawn in the shop.",
            "How many grenades you can purchase total. Set 0 to disable",
            "How many of each mine to spawn in the shop.",
            "How many mines you can purchase total. Set 0 to disable",
            "How many of each health-pack to spawn in the shop.",
            "How many health-packs you can purchase total. Set 0 to disable",
            "How many of each drone to spawn in the shop.",
            "How many drones you can purchase total. Set 0 to disable",
            "How many of each orb to spawn in the shop.",
            "How many orbs you can purchase total. Set 0 to disable",
            "How many of each crystal to spawn in the shop.",
            "How many crystals you can purchase total. Set 0 to disable",
            "How many trackers to spawn in the shop.",
            "How many trackers you can purchase total. Set 0 to disable",
            "Overrides the values (MaxAmountInShop, MaxPurchaseAmount) set by other item/upgrade mods.",
            "Overrides the values (MaxAmountInShop, MaxPurchaseAmount) of single-use upgrades.",
            "Spawns the additional shelving into the shop (set false to disable the shelf spawning).",
            "How many additional shelving units to spawn in the shop. Set 0 to disable.",
            "Allows stackable items to be added to the shop",
        };

        public static string[] GetConfigDescriptions() => ConfigEntries.DESCRIPTIONS;
    }
    public class ConfigHelper
    {
        public static ConfigEntry<bool> CreateConfig(
          string section,
          string name,
          bool value,
          string description,
          int min,
          int max)
        {
            return Plugin.Instance.Config.Bind<bool>(section, name, value, new ConfigDescription(description, null, Array.Empty<object>()));
        }

        public static ConfigEntry<int> CreateConfig(
          string section,
          string name,
          int value,
          string description,
          int min,
          int max)
        {
            return Plugin.Instance.Config.Bind<int>(section, name, value, new ConfigDescription(description, (AcceptableValueBase)new AcceptableValueRange<int>(min, max), Array.Empty<object>()));
        }
    }
}
