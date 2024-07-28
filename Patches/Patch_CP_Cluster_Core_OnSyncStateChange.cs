using ChainedPuzzles;
using GameData;
using GTFO.API.Extensions;
using HarmonyLib;
using ScanPosOverride.Managers;

namespace ScanPosOverride.Patches
{
    [HarmonyPatch]
    internal static class Patch_CP_Cluster_Core_OnSyncStateChange
    {
        // NOTE: moved to EOS and has been released
        //    // vanilla bug fix: CP_Cluster_Core.OnPuzzleDone is executed on checkpoint restore
        //    [HarmonyPrefix]
        //    [HarmonyPatch(typeof(CP_Cluster_Core), nameof(CP_Cluster_Core.OnSyncStateChange))]
        //    private static bool Pre_CheckEventsOnPuzzleSolved(CP_Cluster_Core __instance, 
        //        eClusterStatus newStatus, bool isDropinState)
        //    {
        //        pClusterState currentState = __instance.m_sync.GetCurrentState();

        //        // CP_Cluster_Core checkpoint restore fix
        //        if (isDropinState && newStatus == eClusterStatus.Finished)
        //        {
        //            __instance.m_spline.SetVisible(false);
        //            for (int k = 0; k < __instance.m_childCores.Length; k++)
        //            {
        //                __instance.m_childCores[k].Deactivate();
        //            }

        //            // NOTE: unwanted line in R6 mono
        //            // HOWEVER I don't know if this would break any shit
        //            //__instance.OnPuzzleDone?.Invoke(__instance.m_puzzleIndex);
        //            return false;
        //        }

        //        // repeatable command event fix: 
        //        else if (!isDropinState && currentState.status == eClusterStatus.Finished && newStatus == eClusterStatus.SplineReveal)
        //        {
        //            __instance.m_spline.Reveal();
        //            return false;
        //        }

        //        return true;
        //    }

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
