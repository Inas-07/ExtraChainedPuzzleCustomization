﻿using ChainedPuzzles;
using GameData;
using GTFO.API.Extensions;
using HarmonyLib;
using ScanPosOverride.Component;
using ScanPosOverride.Managers;
using ScanPosOverride.PuzzleOverrideData;
using System;
using UnityEngine;

namespace ScanPosOverride.Patches
{
    [HarmonyPatch]
    internal class Patch_CP_Cluster_Core_Setup
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(CP_Cluster_Core), nameof(CP_Cluster_Core.Setup))]
        private static void Pre_CP_Cluster_Core_Setup(
            CP_Cluster_Core __instance, int puzzleIndex, iChainedPuzzleOwner owner,
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
                    CP_Cluster_Core lastClusterPuzzle = scanOwner.m_chainedPuzzleCores[puzzleIndex - 1].TryCast<CP_Cluster_Core>();
                    if (lastClusterPuzzle == null)
                    {
                        SPOLogger.Error($"Cannot cast m_chainedPuzzleCores[{puzzleIndex - 1}] to neither CP_Bioscan_Core or CP_Cluster_Core! WTF???");
                    }

                    else prevPuzzlePos = lastClusterPuzzle.transform.position;
                }
            }

            // -----------------------------------------
            //   modify clustering position
            // -----------------------------------------
            uint puzzleOverrideIndex = PuzzleOverrideManager.Current.Register(__instance);
            PuzzleOverride puzzleOverride = Plugin.GetOverride(PuzzleOverrideManager.MainLevelLayout, puzzleOverrideIndex);
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

            SPOLogger.Warning("Overriding CP_Cluster_Core!");
        }

        // handle cluster T-scan
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CP_Cluster_Core), nameof(CP_Cluster_Core.Setup))]
        private static void Post_CP_Cluster_Core_Setup(CP_Cluster_Core __instance)
        {
            foreach(var childCore in __instance.m_childCores)
            {
                if (!childCore.IsMovable) continue;
                uint puzzleOverrideIndex = PuzzleOverrideManager.Current.GetBioscanCoreOverrideIndex(childCore.Pointer);
                if (puzzleOverrideIndex == 0) continue;

                PuzzleOverride TScanPositions = Plugin.GetOverride(PuzzleOverrideManager.MainLevelLayout, puzzleOverrideIndex);

                if (TScanPositions == null || TScanPositions.TPositions == null || TScanPositions.TPositions.Count < 1)
                {
                    SPOLogger.Error("No Override for this T-Scan, falling back to vanilla impl.");
                    continue;
                }

                CP_Bioscan_Core TScanCore = new CP_Bioscan_Core(childCore.Pointer);

                if(TScanCore.m_movingComp == null)
                {
                    Debug.LogError("Chained puzzle instance set to movable but does not include iChainedPuzzleMovable.");
                }
                else if (TScanCore.m_movingComp.UsingStaticBioscanPoints)
                {
                    foreach (var pos in TScanPositions.TPositions)
                        TScanCore.m_movingComp.ScanPositions.Add(pos.ToVector3());

                    TScanCore.transform.position = TScanPositions.TPositions[0].ToVector3();

                    // disable the holopath after Setup() complete.
                    __instance.m_revealWithHoloPath = false;
                    SPOLogger.Warning("Overriding T-Scan pos!");
                }
                else
                {
                    Debug.LogError("Unimplemented.");
                    // Lazy. No impl.
                }
            }

            uint overrideIndex = PuzzleOverrideManager.Current.GetClusterCoreOverrideIndex(__instance);
            if (overrideIndex == 0) return;

            PuzzleOverride def = Plugin.GetOverride(PuzzleOverrideManager.MainLevelLayout, overrideIndex);
            if(def == null) return;

            if(def.ConcurrentCluster)
            {
                if (2 <= __instance.m_childCores.Count && __instance.m_childCores.Count <= 4)
                {
                    var parentHud = __instance.m_hud.Cast<CP_Bioscan_Hud>();
                    var cchud = parentHud.gameObject.AddComponent<ConcurrentClusterHud>();
                    cchud.parent = __instance;
                    cchud.parentHud = parentHud;
                    cchud.def = def;
                    if(cchud.Setup())
                    {
                        PlayerScannerManager.Current.RegisterConcurrentCluster(__instance);
                        SPOLogger.Warning("Setting up CP_Cluster_Core as Concurrent Cluster!");
                    }
                    else
                    {
                        SPOLogger.Warning("Concurrent Cluster: faild to setup");
                    }
                }

                else
                {
                    SPOLogger.Error("Trying to setup concurrent cluster, " +
                        $"but the cluster scan has {__instance.m_childCores.Count}, which is senseless or is impossible for 4 players to complete");
                }
            }

            if(def.EventsOnClusterProgress.Count > 0)
            {
                foreach(var child in __instance.m_childCores)
                {
                    child.add_OnPuzzleDone(new System.Action<int>(CheckEventsOnClusterProgress));
                }

                void CheckEventsOnClusterProgress(int puzzleIndex)
                {
                    int cnt = 0;
                    for (int i = 0; i < __instance.m_childCores.Length; i++)
                    {
                        if (__instance.m_childCores[i].IsFinished())
                        {
                            cnt += 1;
                        }
                    }

                    foreach(var eop in def.EventsOnClusterProgress)
                    {
                        if (eop.Count == cnt)
                        {
                            WardenObjectiveManager.CheckAndExecuteEventsOnTrigger(eop.Events.ToIl2Cpp(), eWardenObjectiveEventTrigger.None, true);
                        }
                    }
                }
            }
        }
    }
}
