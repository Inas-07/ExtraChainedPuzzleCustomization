using ChainedPuzzles;
using GameData;
using GTFO.API.Extensions;
using HarmonyLib;
using ScanPosOverride.Managers;

namespace ScanPosOverride.Patches
{
    [HarmonyPatch]
    internal static class Patch_CP_Cluster_Core_OnSyncStateChaged
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CP_Cluster_Core), nameof(CP_Cluster_Core.OnSyncStateChange))]
        private static void Post_CheckEventsOnPuzzleSolved(CP_Cluster_Core __instance, eClusterStatus newStatus, bool isDropinState)
        {
            if (newStatus != eClusterStatus.Finished) return;

            var overrideIndex = PuzzleOverrideManager.Current.GetClusterCoreOverrideIndex(__instance);
            var def = Plugin.GetOverride(PuzzleOverrideManager.MainLevelLayout, overrideIndex);
            if (def == null) return;

            if(def.EventsOnPuzzleSolved.Count > 0 && !isDropinState)
            {
                WardenObjectiveManager.CheckAndExecuteEventsOnTrigger(def.EventsOnPuzzleSolved.ToIl2Cpp(), eWardenObjectiveEventTrigger.None, true);
            }
        }
    }
}
