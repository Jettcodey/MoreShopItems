using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MoreShopItems;

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
}
