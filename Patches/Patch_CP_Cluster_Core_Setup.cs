using ChainedPuzzles;
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
            uint puzzleOverrideIndex = PuzzleOverrideManager.Current.Register(__instance);
            PuzzleOverride def = Plugin.GetOverride(PuzzleOverrideManager.MainLevelLayout, puzzleOverrideIndex);

            ChainedPuzzleInstance scanOwner = owner.Cast<ChainedPuzzleInstance>();

            // -----------------------------------------
            //           modify `prevPuzzlePos`.
            // we don't want to use the (random / static) position 
            // from ChainedPuzzleInstance.Setup() 
            // if last puzzle is overriden.
            // will affect vanilla setup as well, but nothing would break if works.
            // -----------------------------------------
            if (def != null && def.PrevPosOverride.ToVector3() != Vector3.zero)
            {
                prevPuzzlePos = def.PrevPosOverride.ToVector3();
            }
            else if (def != null && def.PrevPosOverrideIndex > 0)
            {
                var overridePosition = PuzzleOverrideManager.Current.GetBioscanCore(def.PrevPosOverrideIndex)?.m_position
                    ?? PuzzleOverrideManager.Current.GetClusterCore(def.PrevPosOverrideIndex)?.transform.position
                    ?? prevPuzzlePos; // default to what it already was if getting either of the previous fails
                prevPuzzlePos = overridePosition;

            }
            else
            {
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

                        else
                        {
                            prevPuzzlePos = lastClusterPuzzle.transform.position;
                        }
                    }
                }
            }

            // -----------------------------------------
            //   modify clustering position
            // -----------------------------------------
            if (def == null) return;

            if (def.Position.x != 0.0 || def.Position.y != 0.0 || def.Position.z != 0.0
                || def.Rotation.x != 0.0 || def.Rotation.y != 0.0 || def.Rotation.z != 0.0)
            {
                __instance.transform.SetPositionAndRotation(def.Position.ToVector3(), def.Rotation.ToQuaternion());
            }

            if (def.EventsOnPuzzleSolved != null && def.EventsOnPuzzleSolved.Count > 0)
            {
                __instance.add_OnPuzzleDone(new Action<int>((_) => WardenObjectiveManager.CheckAndExecuteEventsOnTrigger(def.EventsOnPuzzleSolved.ToIl2Cpp(), 
                    eWardenObjectiveEventTrigger.None, true)));
            }

            if (def.RequiredItemsIndices != null && def.RequiredItemsIndices.Count > 0)
            {
                PuzzleReqItemManager.Current.QueueForAddingReqItems(__instance, def.RequiredItemsIndices);
            }

            // no spline for T scan
            // prolly work for "clustered T-scan" as well?
            if (def.HideSpline)
            {
                revealWithHoloPath = false;
            }

            SPOLogger.Warning("Overriding CP_Cluster_Core!");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CP_Cluster_Core), nameof(CP_Cluster_Core.Setup))]
        private static void Post_CP_Cluster_Core_Setup(CP_Cluster_Core __instance)
        {
            // =============================================
            //              handle cluster T-scan
            // =============================================
            ChainedPuzzleInstance chainedPuzzle = __instance.m_owner.Cast<ChainedPuzzleInstance>();
            foreach (var childCore in __instance.m_childCores)
            {
                if (!childCore.IsMovable) continue;
                uint puzzleOverrideIndex = PuzzleOverrideManager.Current.GetBioscanCoreOverrideIndex(childCore.Pointer);
                if (puzzleOverrideIndex == 0) continue;

                chainedPuzzle.SetupMovement(childCore.Cast<CP_Bioscan_Core>().gameObject, chainedPuzzle.m_sourceArea);
            }

            uint overrideIndex = PuzzleOverrideManager.Current.GetClusterCoreOverrideIndex(__instance);
            if (overrideIndex == 0) return;

            PuzzleOverride def = Plugin.GetOverride(PuzzleOverrideManager.MainLevelLayout, overrideIndex);
            if(def == null) return;

            // =============================================
            //              concurrent cluster
            // =============================================
            if (def.ConcurrentCluster)
            {
                if (2 <= __instance.m_childCores.Count && __instance.m_childCores.Count <= 4)
                {
                    var parentHud = __instance.m_hud.Cast<CP_Cluster_Hud>();
                    var cchud = parentHud.gameObject.AddComponent<ConcurrentClusterHud>();
                    cchud.parent = __instance;
                    cchud.parentHud = parentHud.m_hud.Cast<CP_Bioscan_Hud>();
                    cchud.def = def;
                    if(cchud.Setup())
                    {
                        PlayerScannerManager.Current.RegisterConcurrentCluster(__instance);
                        SPOLogger.Warning("Setting up CP_Cluster_Core as Concurrent Cluster!");
                    }
                    else
                    {
                        SPOLogger.Warning("Concurrent Cluster: failed to setup");
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
                    child.add_OnPuzzleDone(new Action<int>(CheckEventsOnClusterProgress));
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
