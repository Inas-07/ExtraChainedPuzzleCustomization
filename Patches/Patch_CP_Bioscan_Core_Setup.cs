using ChainedPuzzles;
using HarmonyLib;
using LevelGeneration;
using UnityEngine;
using ScanPosOverride.PuzzleOverrideData;
using GameData;
using ScanPosOverride.Managers;

using Il2cppPlayerList = Il2CppSystem.Collections.Generic.List<Player.PlayerAgent>;
using Il2cppBoolArray = Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<bool>;
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

            // ========================================
            //              single scan 
            // ========================================
            if (scanOwner != null)
            {
                // -----------------------------------------
                //           modify `prevPuzzlePos`.
                // we don't want to use the (random / static) position 
                // from ChainedPuzzleInstance.Setup() 
                // if last puzzle is overriden.
                // will affect vanilla setup as well, but nothing would break if works.
                // -----------------------------------------
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
                        else prevPuzzlePos = lastClusterPuzzle.transform.position;
                    }
                }
            }

            // ========================================
            //              clustered scan 
            // ========================================
            else
            {
                CP_Cluster_Core clusterOwner = owner.TryCast<CP_Cluster_Core>();
                if (clusterOwner == null)
                {
                    SPOLogger.Error("Onwer is not neither ChainedPuzzleInstance nor CP_Cluster_Core. What r u?");
                    return;
                }

                prevPuzzlePos = clusterOwner.transform.position;

                scanOwner = clusterOwner.m_owner.TryCast<ChainedPuzzleInstance>();
                if(scanOwner == null)
                {
                    SPOLogger.Error("Failed to cast clusterOwner.m_owner to ChainedPuzzleInstance");
                    return;
                }

                if(scanOwner.Data.OnlyShowHUDWhenPlayerIsClose == true)
                {
                    onlyShowHUDWhenPlayerIsClose = true;
                }
            }

            // -----------------------------------------
            //   modify this puzzle position / rotation
            // -----------------------------------------
            uint puzzleOverrideIndex = PuzzleOverrideManager.Current.Register(__instance);
            PuzzleOverride puzzleOverride = Plugin.GetOverride(PuzzleOverrideManager.MainLevelLayout, puzzleOverrideIndex);

            // No override. use vanilla
            if (puzzleOverride == null) return;

            if (puzzleOverride.Position.x != 0.0 || puzzleOverride.Position.y != 0.0 || puzzleOverride.Position.z != 0.0
                || puzzleOverride.Rotation.x != 0.0 || puzzleOverride.Rotation.y != 0.0 || puzzleOverride.Rotation.z != 0.0)
            {
                __instance.transform.SetPositionAndRotation(puzzleOverride.Position.ToVector3(), puzzleOverride.Rotation.ToQuaternion());
            }

            if (puzzleOverride.EventsOnPuzzleSolved != null && puzzleOverride.EventsOnPuzzleSolved.Count > 0) 
            {
                __instance.add_OnPuzzleDone(new System.Action<int>((i) => {
                    foreach(WardenObjectiveEventData e in puzzleOverride.EventsOnPuzzleSolved)
                    {
                        WardenObjectiveManager.CheckAndExecuteEventsOnTrigger(e, eWardenObjectiveEventTrigger.None, true);
                    }
                }));
            }

            // no spline for T scan
            // prolly work for "clustered T-scan" as well?
            if (puzzleOverride.HideSpline ||  __instance.m_movingComp != null && __instance.m_movingComp.IsMoveConfigured)
            {
                revealWithHoloPath = false;
            }

            if (puzzleOverride.RequiredItemsIndices != null && puzzleOverride.RequiredItemsIndices.Count > 0)
            {
                PuzzleReqItemManager.Current.QueueForAddingReqItems(__instance, puzzleOverride.RequiredItemsIndices);
            }
            
            SPOLogger.Warning("Overriding CP_Bioscan_Core." + (scanOwner == null ? "" : $"Zone {scanOwner.m_sourceArea.m_zone.Alias}, Layer {scanOwner.m_sourceArea.m_zone.Layer.m_type}, Dim {scanOwner.m_sourceArea.m_zone.DimensionIndex}"));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CP_Bioscan_Core), nameof(CP_Bioscan_Core.Setup))]
        private static void Post_CP_Bioscan_Core_Setup(CP_Bioscan_Core __instance)
        {
            uint puzzleOverrideIndex = PuzzleOverrideManager.Current.GetBioscanCoreOverrideIndex(__instance);
            PuzzleOverride def = Plugin.GetOverride(PuzzleOverrideManager.MainLevelLayout, puzzleOverrideIndex);

            //TODO: execute events on progress 
            if (def != null)
            {
                int i = 0; // def.EventsOnProgress[i] are the events that should be executed next

                __instance.m_sync.add_OnSyncStateChange(new System.Action<eBioscanStatus, float, Il2cppPlayerList, int, Il2cppBoolArray, bool>(CheckBioscanEventsOnProgress));
                
                void CheckBioscanEventsOnProgress(eBioscanStatus status, float progress,
                    Il2cppPlayerList playersInScan, int playerMax, Il2cppBoolArray reqItemStatus,
                    bool isDropinState)
                {
                    if (isDropinState)
                    {
                        while (i < def.EventsOnBioscanProgress.Count)
                        {
                            var curEOP = def.EventsOnBioscanProgress[i];
                            if (curEOP.Progress > progress)
                            {
                                break;
                            }
                            else
                            {
                                i++;
                            }
                        }
                        return;
                    }

                    if (i < def.EventsOnBioscanProgress.Count)
                    {
                        var curEOP = def.EventsOnBioscanProgress[i];
                        if (curEOP.Progress < progress) 
                        {
                            WardenObjectiveManager.CheckAndExecuteEventsOnTrigger(def.EventsOnBioscanProgress[i].Events.ToIl2Cpp(), eWardenObjectiveEventTrigger.None, true);
                            i += 1;
                        }
                    }
                }
            }
        }
    }
}
