using ChainedPuzzles;
using HarmonyLib;
using Il2CppSystem.Collections.Generic;
using LevelGeneration;
using Player;
using UnityEngine;
using GTFO.API;
namespace ScanPosOverride.Patches
{
    [HarmonyPatch]
    internal class Patch_CP_Bioscan_Core_OnSyncStateChange
    {
        private static System.Collections.Generic.Dictionary<System.IntPtr, CP_PlayerScanner> cachedScanner = new();


        // TODO: implementation of T-Scan moving policy && Concurrent cluster scan should both fall into this method.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CP_Bioscan_Core), nameof(CP_Bioscan_Core.OnSyncStateChange))]
        private static void Post_CP_Bioscan_Core_OnSyncStateChanged(CP_Bioscan_Core __instance, 
            eBioscanStatus status, List<PlayerAgent> playersInScan, 
            bool[] reqItemStatus)
        {
            if (!__instance.IsMovable || status != eBioscanStatus.Scanning) return;

            CP_PlayerScanner scanner;
            if(!cachedScanner.ContainsKey(__instance.Pointer))
            {
                scanner = __instance.m_playerScanner.TryCast<CP_PlayerScanner>();
                if(scanner == null)
                {
                    Logger.Error("Failed to cast to CP_PlayerScanner!");
                }

                // will add null scanner
                cachedScanner.Add(__instance.Pointer, scanner);
            }
            else
            {
                scanner = cachedScanner[__instance.Pointer];
            }

            if(scanner == null)
            {
                Logger.Error("Null CP_PlayerScanner");
                return;
            }

            int playersInScanCount = playersInScan.Count;
            var playerAgentsInLevel = PlayerManager.PlayerAgentsInLevel;

            float scanSpeed = 0.0f;
            if (__instance.m_playerScanner.ScanPlayersRequired == PlayerRequirement.None)
            {
                scanSpeed = playersInScanCount <= 0 ? 0.0f : scanner.m_scanSpeeds[playersInScanCount - 1];
            }
            else if (scanner.m_playerRequirement == PlayerRequirement.All && playersInScanCount == playerAgentsInLevel.Count
                || (scanner.m_playerRequirement == PlayerRequirement.Solo && playersInScanCount == 1))
            {
                scanSpeed = scanner.m_scanSpeeds[0];
            }

            bool hasPositiveScanSpeed = scanSpeed > 0.0f;
            bool ScanShouldProgress = true;

            if (!hasPositiveScanSpeed)
            {
                ScanShouldProgress = false;
            }

            else if (scanner.m_reqItemsEnabled) 
            {
                for (int index = 0; index < scanner.m_reqItems.Length; ++index)
                {
                    // NOTE: Do not use `reqItemStatus` - buggy from Il2Cpp
                    Vector3 vector3_2 = Vector3.zero;
                    if (scanner.m_reqItems[index].PickupItemStatus == ePickupItemStatus.PlacedInLevel)
                        vector3_2 = scanner.m_reqItems[index].transform.position;
                    else if (scanner.m_reqItems[index].PickupItemStatus == ePickupItemStatus.PickedUp)
                    {
                        if (scanner.m_reqItems[index].PickedUpByPlayer != null)
                            vector3_2 = scanner.m_reqItems[index].PickedUpByPlayer.Position;
                        else
                            Debug.LogError("Playerscanner is looking for an item that has ePickupItemStatus.PickedUp but null player, how come!?");
                    }
                    Vector3 vec = scanner.transform.position - vector3_2;
                    if (vec.sqrMagnitude >= scanner.m_scanRadiusSqr)
                    {
                        ScanShouldProgress = false;
                        break;
                    }
                }
            }

            if (ScanShouldProgress)
            {
                __instance.m_movingComp.ResumeMovement();
            }
            else
            {
                __instance.m_movingComp.PauseMovement();
            }
        }

        static Patch_CP_Bioscan_Core_OnSyncStateChange()
        {
            LevelAPI.OnLevelCleanup += cachedScanner.Clear;
        }
    }
}
