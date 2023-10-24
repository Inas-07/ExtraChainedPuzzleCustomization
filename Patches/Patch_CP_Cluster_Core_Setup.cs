using ChainedPuzzles;
using GameData;
using HarmonyLib;
using LevelGeneration;
using ScanPosOverride.Managers;
using ScanPosOverride.PuzzleOverrideData;
using UnityEngine;

namespace ScanPosOverride.Patches
{
    [HarmonyPatch]
    internal class Patch_CP_Cluster_Core_Setup
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CP_Cluster_Core), nameof(CP_Cluster_Core.Setup))]
        private static void Pre_CP_Cluster_Core_Setup(
            CP_Cluster_Core __instance, int puzzleIndex, iChainedPuzzleOwner owner, LG_Area sourceArea,
            ref Vector3 prevPuzzlePos, ref bool revealWithHoloPath)
        {
            ChainedPuzzleInstance scanOwner = new ChainedPuzzleInstance(owner.Pointer);

            // -----------------------------------------
            //           modify `prevPuzzlePos`.
            // we don't want to use the (random / static) position 
            // from ChainedPuzzleInstance.Setup() 
            // if last puzzle is overriden.
            // will affect vanilla setup as well, but nothing would break if works.
            // -----------------------------------------
            if (puzzleIndex == 0)
            {

            }
            else
            {
                // prevPuzzlePos should be position of last scan.
                CP_Bioscan_Core lastSinglePuzzle = scanOwner.m_chainedPuzzleCores[puzzleIndex - 1].TryCast<CP_Bioscan_Core>();
                if (lastSinglePuzzle != null)
                {
                    prevPuzzlePos = lastSinglePuzzle.transform.position;
                }
                else
                {
                    CP_Cluster_Core lastClusterPuzzle = scanOwner.m_chainedPuzzleCores[puzzleIndex - 1].Cast<CP_Cluster_Core>();
                    if (lastClusterPuzzle == null)
                    {
                        ScanPosOverrideLogger.Error($"Cannot cast m_chainedPuzzleCores[{puzzleIndex - 1}] to neither CP_Bioscan_Core or CP_Cluster_Core! WTF???");
                    }

                    else prevPuzzlePos = lastClusterPuzzle.transform.position;
                }
            }

            // -----------------------------------------
            //   modify clustering position
            // -----------------------------------------
            uint puzzleOverrideIndex = PuzzleInstanceManager.Current.Register(__instance, sourceArea.m_courseNode);
            PuzzleOverride puzzleOverride = Plugin.GetOverride(PuzzleInstanceManager.MainLevelLayout, puzzleOverrideIndex);
            if (puzzleOverride == null) return;

            if (puzzleOverride.Position.x != 0.0 || puzzleOverride.Position.y != 0.0 || puzzleOverride.Position.z != 0.0
                || puzzleOverride.Rotation.x != 0.0 || puzzleOverride.Rotation.y != 0.0 || puzzleOverride.Rotation.z != 0.0)
            {
                __instance.transform.SetPositionAndRotation(puzzleOverride.Position.ToVector3(), puzzleOverride.Rotation.ToQuaternion());
            }

            if (puzzleOverride.EventsOnPuzzleSolved != null && puzzleOverride.EventsOnPuzzleSolved.Count > 0)
            {
                __instance.add_OnPuzzleDone(new System.Action<int>((i) => {
                    foreach (WardenObjectiveEventData e in puzzleOverride.EventsOnPuzzleSolved)
                    {
                        WardenObjectiveManager.CheckAndExecuteEventsOnTrigger(e, eWardenObjectiveEventTrigger.None, true);
                    }
                }));
            }

            if (puzzleOverride.RequiredItemsIndices != null && puzzleOverride.RequiredItemsIndices.Count > 0)
            {
                PuzzleReqItemManager.Current.QueueForAddingReqItems(__instance, puzzleOverride.RequiredItemsIndices);
            }

            // no spline for T scan
            // prolly work for "clustered T-scan" as well?
            if (puzzleOverride.HideSpline)
            {
                revealWithHoloPath = false;
            }

            ScanPosOverrideLogger.Warning("Overriding CP_Cluster_Core!");
        }

        // handle cluster T-scan
        [HarmonyPostfix] // NOTE: all stuff has been setup properly
        [HarmonyPatch(typeof(CP_Cluster_Core), nameof(CP_Cluster_Core.Setup))]
        private static void Post_CP_Cluster_Core_Setup(CP_Cluster_Core __instance)
        {
            foreach(var childCore in __instance.m_childCores)
            {
                if (!childCore.IsMovable) continue;
                uint puzzleOverrideIndex = PuzzleInstanceManager.Current.GetZoneInstanceIndex(childCore.Cast<CP_Bioscan_Core>()); 
                if (puzzleOverrideIndex == 0) continue;

                PuzzleOverride clusterTOverride = Plugin.GetOverride(PuzzleInstanceManager.MainLevelLayout, puzzleOverrideIndex);

                if (clusterTOverride == null || clusterTOverride.TPositions == null || clusterTOverride.TPositions.Count < 1)
                {
                    ScanPosOverrideLogger.Error("No Override for this T-Scan, falling back to vanilla impl.");
                    continue;
                }

                CP_Bioscan_Core TScanCore = new CP_Bioscan_Core(childCore.Pointer);

                if(TScanCore.m_movingComp == null)
                {
                    Debug.LogError("Chained puzzle instance set to movable but does not include iChainedPuzzleMovable.");
                }
                else if (TScanCore.m_movingComp.UsingStaticBioscanPoints)
                {
                    foreach (var pos in clusterTOverride.TPositions)
                        TScanCore.m_movingComp.ScanPositions.Add(pos.ToVector3());

                    TScanCore.transform.position = clusterTOverride.TPositions[0].ToVector3();
                    
                    if (clusterTOverride.TMoveSpeedMulti > 0.0)
                    {
                        var TMovableComp = TScanCore.m_movingComp.Cast<CP_BasicMovable>();
                        TMovableComp.m_movementSpeed *= clusterTOverride.TMoveSpeedMulti; 
                    }

                    // disable the holopath after Setup() complete.
                    __instance.m_revealWithHoloPath = false;
                    ScanPosOverrideLogger.Warning("Overriding T-Scan pos!");
                }
                else
                {
                    Debug.LogError("Unimplemented.");
                    // Lazy. No impl.
                }
            }

            uint overrideIndex = PuzzleInstanceManager.Current.GetZoneInstanceIndex(__instance);
            if (overrideIndex == 0) return;

            PuzzleOverride overrideData = Plugin.GetOverride(PuzzleInstanceManager.MainLevelLayout, overrideIndex);
            if(overrideData == null || overrideData.ConcurrentCluster == false) return;

            PlayerScannerManager.Current.RegisterConcurrentCluster(__instance);
            ScanPosOverrideLogger.Warning("Setting up CP_Cluster_Core as Concurrent Cluster!");
        }
    }
}
