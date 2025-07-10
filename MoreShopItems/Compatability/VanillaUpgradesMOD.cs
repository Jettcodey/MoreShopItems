using System;
using System.Text;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace MoreShopItems.Compatability
{
    internal static class VanillaUpgradesMOD
    {
        public static bool isLoaded() =>
            Chainloader.PluginInfos.ContainsKey("bulletbot.vanillaupgrades");
    }
}

