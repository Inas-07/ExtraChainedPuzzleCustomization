using ChainedPuzzles;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System.Collections.Generic;
using GTFO.API;
using System.Linq;

namespace ScanPosOverride.Patches
{
    [HarmonyPatch]
    internal class Patches_CP_Cluster_Hud_ReqItems
    {
        // System.IntPtr is Cluster hud
        private static Dictionary<System.IntPtr, List<bool>> childrenReqItemEnabled { get; } = new();

        private static Dictionary<System.IntPtr, List<string[]>> clustersChildrenReqItemNames { get; } = new();

        // patch for SetRequiredItemData
        private static Dictionary<System.IntPtr, List<Il2CppStructArray<bool>>> clustersChildrenReqItemsStatus { get; } = new();

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CP_Bioscan_Core), nameof(CP_Bioscan_Core.AddRequiredItems))]
        private static void Post_CP_Bioscan_Core_AddRequiredItems(CP_Bioscan_Core __instance, Il2CppReferenceArray<iWardenObjectiveItem> requiredItems)
        {
            CP_Cluster_Core parent = __instance.Owner.TryCast<CP_Cluster_Core>();
            if (parent == null) return;

            if (__instance.m_hud == null)
            {
                SPOLogger.Error("CP_Cluster_Hud_ReqItems: replacement Cluster_hud is null.");
                return;
            }

            CP_Cluster_Hud hud = __instance.m_hud.Cast<CP_Cluster_Hud>();

            string[] reqItemNames = new string[requiredItems.Count];
            for (int index = 0; index < __instance.m_reqItems.Count; ++index)
            {
                if (__instance.m_reqItems[index] != null)
                    reqItemNames[index] = __instance.m_reqItems[index].PublicName;
                else
                    SPOLogger.Error("Post_CP_Bioscan_Core_AddRequiredItems: CP_Bioscan_Core " + __instance.name + " has a missing m_reqItem! " + index);
            }

            List<bool> reqItemEnabled;
            List<string[]> clusterReqItemNames;
            if(childrenReqItemEnabled.ContainsKey(hud.Pointer))
            {
                reqItemEnabled = childrenReqItemEnabled[hud.Pointer];
                clusterReqItemNames = clustersChildrenReqItemNames[hud.Pointer];
            }
            else
            {
                reqItemEnabled = Enumerable.Repeat(false, parent.NRofPuzzles()).ToList();
                clusterReqItemNames = Enumerable.Repeat(new string[0], parent.NRofPuzzles()).ToList();
                childrenReqItemEnabled.Add(hud.Pointer, reqItemEnabled);
                clustersChildrenReqItemNames.Add(hud.Pointer, clusterReqItemNames);
            }

            reqItemEnabled[__instance.m_puzzleIndex] = __instance.m_reqItemsEnabled;
            clusterReqItemNames[__instance.m_puzzleIndex] = reqItemNames;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CP_Cluster_Hud), nameof(CP_Cluster_Hud.SetRequiredItemData))]
        private static bool Pre_CP_Cluster_Hud_SetRequiredItemData(CP_Cluster_Hud __instance, int puzzleIndex /* cluster child index */, Il2CppStructArray<bool> reqItemStatus)
        {
            List<Il2CppStructArray<bool>> childrenReqItems;
            if (!clustersChildrenReqItemsStatus.ContainsKey(__instance.Pointer))
            {
                childrenReqItems = Enumerable.Repeat<Il2CppStructArray<bool>>(null, __instance.m_clusterSize).ToList();
                clustersChildrenReqItemsStatus.Add(__instance.Pointer, childrenReqItems);
            }
            else
            {
                childrenReqItems = clustersChildrenReqItemsStatus[__instance.Pointer];
            }

            childrenReqItems[puzzleIndex] = reqItemStatus;
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CP_Cluster_Hud), nameof(CP_Cluster_Hud.UpdateDataFor))]
        private static void Post_CP_Cluster_Hud_UpdateDataFor(CP_Cluster_Hud __instance, int index)
        {
            if (!clustersChildrenReqItemsStatus.ContainsKey(__instance.Pointer)) return;

            if (!childrenReqItemEnabled.ContainsKey(__instance.Pointer) || !clustersChildrenReqItemNames.ContainsKey(__instance.Pointer))
            {
                SPOLogger.Error("CP_Cluster_Hud_UpdateDataFor: Found registered reqItemStatus but ReqItemEnabled or ReqItemNames is missing!");
                return;
            }

            var reqItemStatus = clustersChildrenReqItemsStatus[__instance.Pointer][index];

            __instance.m_hud.SetupRequiredItems(childrenReqItemEnabled[__instance.Pointer][index], clustersChildrenReqItemNames[__instance.Pointer][index]);
            __instance.m_hud.SetRequiredItemData(__instance.m_puzzleIndex, reqItemStatus);
        }

        private static void Clear()
        {
            clustersChildrenReqItemsStatus.Clear();
            clustersChildrenReqItemNames.Clear();
            childrenReqItemEnabled.Clear();
        }

        static Patches_CP_Cluster_Hud_ReqItems()
        {
            LevelAPI.OnLevelCleanup += Clear;
        }
    }
}