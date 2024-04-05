using ChainedPuzzles;
using HarmonyLib;
using UnityEngine;
using ScanPosOverride.PuzzleOverrideData;
using GameData;
using ScanPosOverride.Managers;
using GTFO.API.Extensions;

namespace ScanPosOverride.Patches
{
    [HarmonyPatch]
    internal class Patch_CP_Bioscan_Core_Setup
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CP_Bioscan_Core), nameof(CP_Bioscan_Core.Setup))]
        private static void Pre_CP_Bioscan_Core_Setup(CP_Bioscan_Core __instance,
            int puzzleIndex, iChainedPuzzleOwner owner, ref Vector3 prevPuzzlePos, ref bool revealWithHoloPath, ref bool onlyShowHUDWhenPlayerIsClose)
        {
            // owner could either be ChainedPuzzleInstance (single scan), or CP_Cluster_Core (clustered scan).
            ChainedPuzzleInstance scanOwner = owner.TryCast<ChainedPuzzleInstance>();
            uint puzzleOverrideIndex = PuzzleOverrideManager.Current.Register(__instance);
            PuzzleOverride def = Plugin.GetOverride(PuzzleOverrideManager.MainLevelLayout, puzzleOverrideIndex);

            // ========================================
            //           modify `prevPuzzlePos`.
            // we don't want to use the (random / static) position 
            // from ChainedPuzzleInstance.Setup() 
            // if last puzzle is overriden.
            // will affect vanilla setup as well, but nothing would break if works.
            // ========================================
            if (scanOwner != null) // single scan 
            {
                if (def != null && def.PrevPosOverride.ToVector3() != Vector3.zero)
                {
                    prevPuzzlePos = def.PrevPosOverride.ToVector3();
                }
                else
                {
                    if (puzzleIndex == 0)
                    {
                        // prevPuzzlePos should be Sec-Door transform. 
                        // Do nothing
                    }
                    else // puzzleIndex > 0
                    {
                        // prevPuzzlePos should be position of last scan.
                        CP_Bioscan_Core lastSinglePuzzle = scanOwner.m_chainedPuzzleCores[puzzleIndex - 1].TryCast<CP_Bioscan_Core>();
                        if (lastSinglePuzzle != null)
                        {
                            prevPuzzlePos = lastSinglePuzzle.transform.position;
                        }
                        else
                        {
                            CP_Cluster_Core lastClusterPuzzle = scanOwner.m_chainedPuzzleCores[puzzleIndex - 1].TryCast<CP_Cluster_Core>();
                            if (lastClusterPuzzle == null)
                            {
                                SPOLogger.Error($"Cannot cast m_chainedPuzzleCores[{puzzleIndex - 1}] to neither CP_Bioscan_Core or CP_Cluster_Core! WTF???");
                            }
                            else
                            {
                                prevPuzzlePos = lastClusterPuzzle.transform.position;
                            }
                        }
                    }
                }
            }

            else // clustered scan 
            {
                CP_Cluster_Core clusterOwner = owner.Cast<CP_Cluster_Core>();

                // -----------------------------------------
                if (def != null && def.PrevPosOverride.ToVector3() != Vector3.zero)
                {
                    prevPuzzlePos = def.PrevPosOverride.ToVector3();
                }
                else
                {
                    prevPuzzlePos = clusterOwner.transform.position;
                }

                scanOwner = clusterOwner.m_owner.Cast<ChainedPuzzleInstance>();
                if(scanOwner.Data.OnlyShowHUDWhenPlayerIsClose == true)
                {
                    onlyShowHUDWhenPlayerIsClose = true;
                }
            }

            // ========================================
            //   modify this puzzle position / rotation
            // ========================================

            // No override. use vanilla
            if (def == null) return;

            if (def.Position.x != 0.0 || def.Position.y != 0.0 || def.Position.z != 0.0
                || def.Rotation.x != 0.0 || def.Rotation.y != 0.0 || def.Rotation.z != 0.0)
            {
                __instance.transform.SetPositionAndRotation(def.Position.ToVector3(), def.Rotation.ToQuaternion());
            }

            if (def.EventsOnPuzzleSolved != null && def.EventsOnPuzzleSolved.Count > 0) 
            {
                __instance.add_OnPuzzleDone(new System.Action<int>((_) => 
                    WardenObjectiveManager.CheckAndExecuteEventsOnTrigger(def.EventsOnPuzzleSolved.ToIl2Cpp(), eWardenObjectiveEventTrigger.None, true)));
            }

            // no spline for T scan
            // prolly work for "clustered T-scan" as well?
            if (def.HideSpline ||  __instance.m_movingComp != null && __instance.m_movingComp.IsMoveConfigured)
            {
                revealWithHoloPath = false;
            }

            if (def.RequiredItemsIndices != null && def.RequiredItemsIndices.Count > 0)
            {
                PuzzleReqItemManager.Current.QueueForAddingReqItems(__instance, def.RequiredItemsIndices);
            }
            
            SPOLogger.Warning("Overriding CP_Bioscan_Core." + (scanOwner == null ? "" : $"Zone {scanOwner.m_sourceArea.m_zone.Alias}, Layer {scanOwner.m_sourceArea.m_zone.Layer.m_type}, Dim {scanOwner.m_sourceArea.m_zone.DimensionIndex}"));
        }

        //[HarmonyPostfix]
        //[HarmonyPatch(typeof(CP_Bioscan_Core), nameof(CP_Bioscan_Core.Setup))]
        //private static void Post_CP_Bioscan_Core_Setup(CP_Bioscan_Core __instance)
        //{
        //    uint puzzleOverrideIndex = PuzzleOverrideManager.Current.GetBioscanCoreOverrideIndex(__instance);
        //    PuzzleOverride def = Plugin.GetOverride(PuzzleOverrideManager.MainLevelLayout, puzzleOverrideIndex);

        //    //TODO: execute events on progress 
        //    if (def != null)
        //    {
        //        int i = 0; // def.EventsOnProgress[i] are the events that should be executed next

        //        type name too long exception. WON'T WORK!
        //        __instance.m_sync.add_OnSyncStateChange(new System.Action<eBioscanStatus, float, Il2cppPlayerList, int, Il2cppBoolArray, bool>(CheckBioscanEventsOnProgress));

        //        void CheckBioscanEventsOnProgress(eBioscanStatus status, float progress,
        //            Il2cppPlayerList playersInScan, int playerMax, Il2cppBoolArray reqItemStatus,
        //            bool isDropinState)
        //        {
        //            if (isDropinState)
        //            {
        //                while (i < def.EventsOnBioscanProgress.Count)
        //                {
        //                    var curEOP = def.EventsOnBioscanProgress[i];
        //                    if (curEOP.Progress > progress)
        //                    {
        //                        break;
        //                    }
        //                    else
        //                    {
        //                        i++;
        //                    }
        //                }
        //                return;
        //            }

        //            if (i < def.EventsOnBioscanProgress.Count)
        //            {
        //                var curEOP = def.EventsOnBioscanProgress[i];
        //                if (curEOP.Progress < progress)
        //                {
        //                    WardenObjectiveManager.CheckAndExecuteEventsOnTrigger(def.EventsOnBioscanProgress[i].Events.ToIl2Cpp(), eWardenObjectiveEventTrigger.None, true);
        //                    i += 1;
        //                }
        //            }
        //        }
        //    }
        //}
    }
}
