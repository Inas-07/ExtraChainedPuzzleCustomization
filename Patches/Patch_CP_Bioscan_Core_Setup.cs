using ChainedPuzzles;
using HarmonyLib;
using LevelGeneration;
using UnityEngine;
using ScanPosOverride.PuzzleOverrideData;
using GameData;
using ScanPosOverride.Managers;

namespace ScanPosOverride.Patches
{
    [HarmonyPatch]
    internal class Patch_CP_Bioscan_Core_Setup
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CP_Bioscan_Core), nameof(CP_Bioscan_Core.Setup))]
        private static void Pre_CP_Bioscan_Core_Setup(CP_Bioscan_Core __instance, LG_Area sourceArea,
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
                            ScanPosOverrideLogger.Error($"Cannot cast m_chainedPuzzleCores[{puzzleIndex - 1}] to neither CP_Bioscan_Core or CP_Cluster_Core! WTF???");
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
                    ScanPosOverrideLogger.Error("Onwer is not neither ChainedPuzzleInstance nor CP_Cluster_Core. What r u?");
                    return;
                }

                prevPuzzlePos = clusterOwner.transform.position;

                scanOwner = clusterOwner.m_owner.TryCast<ChainedPuzzleInstance>();
                if(scanOwner == null)
                {
                    ScanPosOverrideLogger.Error("Failed to cast clusterOwner.m_owner to ChainedPuzzleInstance");
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
            uint puzzleOverrideIndex = PuzzleInstanceManager.Current.Register(__instance, sourceArea.m_courseNode);
            
            var node = sourceArea.m_courseNode;
            var globalZoneIndex = (node.m_dimension.DimensionIndex, node.LayerType, node.m_zone.LocalIndex); // core here has already been setup properly
            PuzzleInstanceDefinition def = PuzzleDefinitionManager.Current.GetDefinition(globalZoneIndex, puzzleOverrideIndex);

            // No override. use vanilla
            if (def == null) return;

            if (def.Position.x != 0.0 || def.Position.y != 0.0 || def.Position.z != 0.0
                || def.Rotation.x != 0.0 || def.Rotation.y != 0.0 || def.Rotation.z != 0.0)
            {
                __instance.transform.SetPositionAndRotation(def.Position.ToVector3(), def.Rotation.ToQuaternion());
            }

            if (def.EventsOnPuzzleSolved != null && def.EventsOnPuzzleSolved.Count > 0) 
            {
                __instance.add_OnPuzzleDone(new System.Action<int>((i) => {
                    foreach(WardenObjectiveEventData e in def.EventsOnPuzzleSolved)
                    {
                        WardenObjectiveManager.CheckAndExecuteEventsOnTrigger(e, eWardenObjectiveEventTrigger.None, true);
                    }
                }));
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
            
            ScanPosOverrideLogger.Warning("Overriding CP_Bioscan_Core." + (scanOwner == null ? "" : $"Zone {scanOwner.m_sourceArea.m_zone.Alias}, Layer {scanOwner.m_sourceArea.m_zone.Layer.m_type}, Dim {scanOwner.m_sourceArea.m_zone.DimensionIndex}"));
        }
    }
}
