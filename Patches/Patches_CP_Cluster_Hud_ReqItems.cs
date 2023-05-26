﻿using ChainedPuzzles;
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
        private static Dictionary<System.IntPtr, List<bool>> clustersChildrenReqItemEnabled = new();
        private static Dictionary<System.IntPtr, List<string[]>> clustersChildrenReqItemNames = new();

        // patch for SetRequiredItemData
        private static Dictionary<System.IntPtr, List<Il2CppStructArray<bool>>> clustersChildrenReqItemsStatus = new();

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CP_Bioscan_Core), nameof(CP_Bioscan_Core.AddRequiredItems))]
        private static void Post_CP_Bioscan_Core_AddRequiredItems(CP_Bioscan_Core __instance, Il2CppReferenceArray<iWardenObjectiveItem> requiredItems)
        {
            CP_Cluster_Core ClusterOwner = __instance.Owner.TryCast<CP_Cluster_Core>();
            if (ClusterOwner == null) return;

            if (__instance.m_hud == null)
            {
                ScanPosOverrideLogger.Error("CP_Cluster_Hud_ReqItems: replacement Cluster_hud is null.");
                return;
            }

            CP_Cluster_Hud clusterHud = __instance.m_hud.TryCast<CP_Cluster_Hud>();
            if (clusterHud == null)
            {
                ScanPosOverrideLogger.Error("CP_Cluster_Hud_ReqItems: Find cluster owner but cannot cast m_hud to CP_Cluster_hud");
                return;
            }

            string[] reqItemNames = new string[requiredItems.Count];
            for (int index = 0; index < __instance.m_reqItems.Count; ++index)
            {
                if (__instance.m_reqItems[index] != null)
                    reqItemNames[index] = __instance.m_reqItems[index].PublicName;
                else
                    ScanPosOverrideLogger.Error("Post_CP_Bioscan_Core_AddRequiredItems: CP_Bioscan_Core " + __instance.name + " has a missing m_reqItem! " + index);
            }

            List<bool> clusterReqItemEnabled;
            List<string[]> clusterReqItemNames;
            if(clustersChildrenReqItemEnabled.ContainsKey(clusterHud.Pointer))
            {
                clusterReqItemEnabled = clustersChildrenReqItemEnabled[clusterHud.Pointer];
                clusterReqItemNames = clustersChildrenReqItemNames[clusterHud.Pointer];
            }
            else
            {
                clusterReqItemEnabled = Enumerable.Repeat(false, ClusterOwner.NRofPuzzles()).ToList();
                clusterReqItemNames = Enumerable.Repeat(new string[0], ClusterOwner.NRofPuzzles()).ToList();
                clustersChildrenReqItemEnabled.Add(clusterHud.Pointer, clusterReqItemEnabled);
                clustersChildrenReqItemNames.Add(clusterHud.Pointer, clusterReqItemNames);
            }

            clusterReqItemEnabled[__instance.m_puzzleIndex] = __instance.m_reqItemsEnabled;
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

            if (!clustersChildrenReqItemEnabled.ContainsKey(__instance.Pointer) || !clustersChildrenReqItemNames.ContainsKey(__instance.Pointer))
            {
                ScanPosOverrideLogger.Error("CP_Cluster_Hud_UpdateDataFor: Found registered reqItemStatus but ReqItemEnabled or ReqItemNames is missing!");
                return;
            }

            var reqItemStatus = clustersChildrenReqItemsStatus[__instance.Pointer][index];

            __instance.m_hud.SetupRequiredItems(clustersChildrenReqItemEnabled[__instance.Pointer][index], clustersChildrenReqItemNames[__instance.Pointer][index]);
            __instance.m_hud.SetRequiredItemData(__instance.m_puzzleIndex, reqItemStatus);
        }

        private static void Clear()
        {
            clustersChildrenReqItemsStatus.Clear();
            clustersChildrenReqItemNames.Clear();
            clustersChildrenReqItemEnabled.Clear();
        }

        static Patches_CP_Cluster_Hud_ReqItems()
        {
            LevelAPI.OnLevelCleanup += Clear;
        }
    }
}