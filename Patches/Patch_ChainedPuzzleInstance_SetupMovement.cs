using ChainedPuzzles;
using HarmonyLib;
using UnityEngine;
using ScanPosOverride.PuzzleOverrideData;

namespace ScanPosOverride.Patches
{
    [HarmonyPatch]
    internal class Patch_ChainedPuzzleInstance_SetupMovement
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ChainedPuzzleInstance), nameof(ChainedPuzzleInstance.SetupMovement))]
        private static bool Pre_SetupMovement(ChainedPuzzleInstance __instance, GameObject gameObject)
        {
            iChainedPuzzleMovable movingComp = gameObject.GetComponent<iChainedPuzzleMovable>();
            if (movingComp == null || !movingComp.UsingStaticBioscanPoints)
            {
                return true;
            }

            iChainedPuzzleCore coreComp = gameObject.GetComponent<iChainedPuzzleCore>();
            CP_Bioscan_Core core = coreComp.TryCast<CP_Bioscan_Core>();

            if(core == null)
            {
                Logger.Error("Pre_SetupMovement: iChainedPuzzleCore -> CP_Bioscan_Core failed");
                return true;
            }

            uint TScanPuzzleIndex = PuzzleOverrideManager.Current.GetBioscanCoreOverrideIndex(core.Pointer);

            if (TScanPuzzleIndex == 0)
            {
                Logger.Error("Did not find registered movable override for this movable scan.");
                return true;
            }

            PuzzleOverride TScanPosition = Plugin.GetOverride(PuzzleOverrideManager.MainLevelLayout, TScanPuzzleIndex);

            if (TScanPosition == null)
            {
                Logger.Error("No Override for this T-Scan, falling back to vanilla impl.");
                return true;
            }

            foreach(var pos in TScanPosition.TPositions)
            {
                movingComp.ScanPositions.Add(pos.ToVector3());
            }

            gameObject.transform.position = TScanPosition.TPositions[0].ToVector3();
            Logger.Warning("Overriding T-Scan pos!");
            return false;
        }
    }
}
