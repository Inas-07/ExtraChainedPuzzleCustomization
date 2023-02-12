using ChainedPuzzles;
using GameData;
using HarmonyLib;
using LevelGeneration;
using ScanPosOverride.Managers;
using ScanPosOverride.PuzzleOverrideData;
using System.Reflection.Metadata.Ecma335;
using UnityEngine;

namespace ScanPosOverride.Patches
{
    [HarmonyPatch]
    internal class Patch_CarryItemPickup_Core_Setup
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CarryItemPickup_Core), nameof(CarryItemPickup_Core.Setup))]
        private static void Post_CarryItemPickup_Core_Setup(CarryItemPickup_Core __instance)
        {
            PuzzleReqItemManager.Current.Register(__instance);
        }
    }
}
