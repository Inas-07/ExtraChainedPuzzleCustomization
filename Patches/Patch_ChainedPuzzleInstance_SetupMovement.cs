using ChainedPuzzles;
using HarmonyLib;
using ScanPosOverride.PuzzleOverrideData;
using ScanPosOverride.Managers;
using LevelGeneration;

namespace ScanPosOverride.Patches
{
    [HarmonyPatch]
    internal class Patch_ChainedPuzzleInstance_SetupMovement
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ChainedPuzzleInstance), nameof(ChainedPuzzleInstance.SetupMovement))]
        private static bool Pre_SetupMovement(ChainedPuzzleInstance __instance, UnityEngine.GameObject gameObject, LG_Area sourceArea)
        {
            iChainedPuzzleMovable movingComp = gameObject.GetComponent<iChainedPuzzleMovable>();
            if (movingComp == null || !movingComp.UsingStaticBioscanPoints)
            {
                return true;
            }

            CP_BasicMovable TComponent = movingComp.Cast<CP_BasicMovable>();
            iChainedPuzzleCore coreComp = gameObject.GetComponent<iChainedPuzzleCore>();
            CP_Bioscan_Core core = coreComp.Cast<CP_Bioscan_Core>();

            uint TScanPuzzleIndex = PuzzleInstanceManager.Current.GetZoneInstanceIndex(core); // At this point core has been setup properly

            if (TScanPuzzleIndex == 0)
            {
                ScanPosOverrideLogger.Error("Did not find registered movable override for this movable scan.");
                return true;
            }

            PuzzleOverride _override = Plugin.GetOverride(PuzzleInstanceManager.MainLevelLayout, TScanPuzzleIndex);

            if (_override == null || _override.TPositions.Count < 1)
            {
                ScanPosOverrideLogger.Error("No Override for this T-Scan, falling back to vanilla impl.");
                return true;
            }

            _override.TPositions.ForEach(pos => movingComp.ScanPositions.Add(pos.ToVector3()));
            gameObject.transform.position = _override.TPositions[0].ToVector3();
            ScanPosOverrideLogger.Warning("Overriding T-Scan pos!");

            TComponent.m_amountOfPositions = _override.TPositions.Count;

            if(_override.TMoveSpeedMulti > 0f)
                TComponent.m_movementSpeed *= _override.TMoveSpeedMulti;
            return false;
        }
    }
}
