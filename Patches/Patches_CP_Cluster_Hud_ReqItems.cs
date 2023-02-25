//using ChainedPuzzles;
//using HarmonyLib;
//using Il2CppInterop.Runtime.InteropTypes.Arrays;
//using System.Collections.Generic;
//using GTFO.API;
//using System.Linq;
//using LevelGeneration;

//namespace ScanPosOverride.Patches
//{
//    [HarmonyPatch]
//    internal class Patches_CP_Cluster_Hud_ReqItems
//    {
//        // System.IntPtr -> Cluster hud
//        // 1 cluster hud per cluster scan
//        // For example: CLASS VII Cluster ALARM owns 7 huds.

//        // patch for SetupRequiredItems
//        private static Queue<int> AddRequiredItems_LastAddedClusterChildIndex = new(1);

//        private static Dictionary<System.IntPtr, List<bool>> clustersChildrenReqItemEnabled = new();
//        private static Dictionary<System.IntPtr, List<Il2CppStringArray>> clustersChildrenReqItemNames = new();

//        // patch for SetRequiredItemData
//        private static Dictionary<System.IntPtr, List<Il2CppStructArray<bool>>> clustersChildrenReqItemsStatus = new();

//        // PREFIX!
//        [HarmonyPrefix]
//        [HarmonyPatch(typeof(CP_Bioscan_Core), nameof(CP_Bioscan_Core.AddRequiredItems))]
//        private static void Pre_CP_Bioscan_Core_AddRequiredItems(CP_Bioscan_Core __instance, Il2CppReferenceArray<iWardenObjectiveItem> requiredItems)
//        {
//            CP_Cluster_Core ClusterOwner = __instance.Owner.TryCast<CP_Cluster_Core>();
//            if (ClusterOwner == null) return;

//            if(__instance.m_hud == null)
//            {
//                Logger.Error("replacement Cluster_hud is null.");
//                return;
//            }

//            AddRequiredItems_LastAddedClusterChildIndex.Enqueue(__instance.m_puzzleIndex);
//        }

//        // immediately invoked before CP_Bioscan_Core.AddRequiredItems ends.
//        // this method is unimplemented on 10cc side
//        // ISSUE: WHY THIS METHOD IS INVOKED BEFORE GAME STARTUP COMPLETE?
//        [HarmonyPostfix]
//        [HarmonyPatch(typeof(CP_Cluster_Hud), nameof(CP_Cluster_Hud.SetupRequiredItems))]
//        private static void Post_CP_Cluster_Hud_SetupRequiredItems(CP_Cluster_Hud __instance, bool enabled, Il2CppStringArray names)
//        {
//            if (AddRequiredItems_LastAddedClusterChildIndex.Count < 1)
//            {
//                Logger.Error("CP_Cluster_Hud_SetupRequiredItems: invoked, but no queued child index.");
//                return;
//            }

//            int puzzleIndex = AddRequiredItems_LastAddedClusterChildIndex.Dequeue();
//            if (puzzleIndex < 0 || puzzleIndex >= __instance.m_clusterSize)
//            {
//                Logger.Error($"Invalid queued child puzzle index {puzzleIndex}");
//                return;
//            }

//            Logger.Warning($"CP_Cluster_Hud.SetupRequiredItems, LastAddedClusterChildIndex: {puzzleIndex}");

//            List<bool> childrenReqItemEnabled;
//            List<Il2CppStringArray> childrenReqItemNames;
//            if (!clustersChildrenReqItemNames.ContainsKey(__instance.Pointer))
//            {
//                childrenReqItemEnabled = Enumerable.Repeat(false, __instance.m_clusterSize).ToList();
//                childrenReqItemNames = Enumerable.Repeat<Il2CppStringArray>(null, __instance.m_clusterSize).ToList();

//                clustersChildrenReqItemEnabled.Add(__instance.Pointer, childrenReqItemEnabled);
//                clustersChildrenReqItemNames.Add(__instance.Pointer, childrenReqItemNames);
//            }
//            else
//            {
//                childrenReqItemEnabled = clustersChildrenReqItemEnabled[__instance.Pointer];
//                childrenReqItemNames = clustersChildrenReqItemNames[__instance.Pointer];
//            }

//            childrenReqItemEnabled[puzzleIndex] = enabled;
//            childrenReqItemNames[puzzleIndex] = names;
//        }

//        [HarmonyPrefix]
//        [HarmonyPatch(typeof(CP_Cluster_Hud), nameof(CP_Cluster_Hud.SetRequiredItemData))]
//        private static bool Pre_CP_Cluster_Hud_SetRequiredItemData(CP_Cluster_Hud __instance, int puzzleIndex /* cluster child index */, Il2CppStructArray<bool> reqItemStatus)
//        {
//            List<Il2CppStructArray<bool>> childrenReqItems;
//            if (!clustersChildrenReqItemsStatus.ContainsKey(__instance.Pointer))
//            {
//                childrenReqItems = Enumerable.Repeat<Il2CppStructArray<bool>>(null, __instance.m_clusterSize).ToList();
//                clustersChildrenReqItemsStatus.Add(__instance.Pointer, childrenReqItems);
//            }
//            else
//            {
//                childrenReqItems = clustersChildrenReqItemsStatus[__instance.Pointer];
//            }

//            childrenReqItems[puzzleIndex] = reqItemStatus;

//            Logger.Warning($"CP_Cluster_Hud_SetRequiredItemData: puzzleIndex {puzzleIndex}, __instance.Pointer {__instance.Pointer}");
//            // this method is unimplemented on 10cc side.
//            // no need to return true
//            return false;
//        }

//        [HarmonyPostfix]
//        [HarmonyPatch(typeof(CP_Cluster_Hud), nameof(CP_Cluster_Hud.UpdateDataFor))]
//        private static void Post_CP_Cluster_Hud_UpdateDataFor(CP_Cluster_Hud __instance, int index)
//        {
//            if (!clustersChildrenReqItemsStatus.ContainsKey(__instance.Pointer)) return;

//            if(!clustersChildrenReqItemEnabled.ContainsKey(__instance.Pointer) || !clustersChildrenReqItemNames.ContainsKey(__instance.Pointer))
//            {
//                Logger.Error("CP_Cluster_Hud_UpdateDataFor: Found registered reqItemStatus but ReqItemEnabled or ReqItemNames is missing!");
//                return;
//            }

//            var reqItemStatus = clustersChildrenReqItemsStatus[__instance.Pointer][index];

//            __instance.m_hud.SetupRequiredItems(clustersChildrenReqItemEnabled[__instance.Pointer][index], clustersChildrenReqItemNames[__instance.Pointer][index]);
//            __instance.m_hud.SetRequiredItemData(__instance.m_puzzleIndex, reqItemStatus);

//            Logger.Warning($"CP_Cluster_Hud.m_hud.SetRequiredItemData: __instance.m_puzzleIndex {__instance.m_puzzleIndex}, index {index}, reqItemStatus == null ? {reqItemStatus == null}");
//        }

//        private static void Clear()
//        {
//            AddRequiredItems_LastAddedClusterChildIndex.Clear();
//            clustersChildrenReqItemsStatus.Clear();
//            clustersChildrenReqItemNames.Clear();
//            clustersChildrenReqItemEnabled.Clear();
//        }

//        static Patches_CP_Cluster_Hud_ReqItems()
//        {
//            LevelAPI.OnLevelCleanup += Clear;
//        }
//    }
//}
