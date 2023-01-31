using ChainedPuzzles;
using HarmonyLib;
using LevelGeneration;
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
            // params that i care about
            CP_Cluster_Core __instance, int puzzleIndex, iChainedPuzzleOwner owner, LG_Area sourceArea, ref Vector3 prevPuzzlePos)
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
            Logger.Warning("Overriding CP_Cluster_Core!");
        }

    }
}
