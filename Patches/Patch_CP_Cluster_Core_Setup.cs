using ChainedPuzzles;
using GameData;
using HarmonyLib;
using LevelGeneration;
using ScanPosOverride.PuzzleOverrideData;
using System.Reflection.Metadata.Ecma335;
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
                        Logger.Error($"Cannot cast m_chainedPuzzleCores[{puzzleIndex - 1}] to neither CP_Bioscan_Core or CP_Cluster_Core! WTF???");
                    }

                    else prevPuzzlePos = lastClusterPuzzle.transform.position;
                }
            }

            // -----------------------------------------
            //   modify clustering position
            // -----------------------------------------
            uint puzzleOverrideIndex = PuzzleOverrideManager.Current.register(__instance);
            PuzzleOverride puzzleOverride = Plugin.GetOverride(PuzzleOverrideManager.MainLevelLayout, puzzleOverrideIndex);
            if (puzzleOverride == null) return;

            __instance.transform.SetPositionAndRotation(puzzleOverride.Position.ToVector3(), puzzleOverride.Rotation.ToQuaternion());
            if (puzzleOverride.EventsOnPuzzleSolved != null && puzzleOverride.EventsOnPuzzleSolved.Count > 0)
            {
                __instance.add_OnPuzzleDone(new System.Action<int>((i) => {
                    foreach (WardenObjectiveEventData e in puzzleOverride.EventsOnPuzzleSolved)
                    {
                        WardenObjectiveManager.CheckAndExecuteEventsOnTrigger(e, eWardenObjectiveEventTrigger.None, true);
                    }
                }));
            }

            Logger.Warning("Overriding CP_Cluster_Core!");
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

                if(TScanPositions == null || TScanPositions.TPositions == null || TScanPositions.TPositions.Count < 1) continue;

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
                    Logger.Warning("Overriding T-Scan pos!");
                }
                else
                {
                    Debug.LogError("Unimplemented.");
                    // Lazy. No impl.
                }
            }
        }
    }
}
