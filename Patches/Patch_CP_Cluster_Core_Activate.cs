using ChainedPuzzles;
using HarmonyLib;
using ScanPosOverride.Managers;

namespace ScanPosOverride.Patches
{
    [HarmonyPatch]
    internal class Patch_CP_Cluster_Core_Activate
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CP_Cluster_Core), nameof(CP_Cluster_Core.Activate))]
        private static void Pre_CP_Cluster_Core_Activate(CP_Cluster_Core __instance)
        {
            if (!PlayerScannerManager.Current.IsConcurrentCluster(__instance)) return;

            if (__instance.m_sync.GetCurrentState().status != eClusterStatus.Disabled
                && __instance.m_sync.GetCurrentState().status != eClusterStatus.Finished) return;


        }
    }
}
