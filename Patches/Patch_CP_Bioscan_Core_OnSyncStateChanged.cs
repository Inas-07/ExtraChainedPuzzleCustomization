using ChainedPuzzles;
using HarmonyLib;
using Il2CppSystem.Collections.Generic;
using LevelGeneration;
using Player;
using UnityEngine;
using ScanPosOverride.Managers;

namespace ScanPosOverride.Patches
{
    [HarmonyPatch]
    internal class Patch_CP_Bioscan_Core_OnSyncStateChange
    {
        // TODO: implementation of T-Scan moving policy && Concurrent cluster scan should both fall into this method.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CP_Bioscan_Core), nameof(CP_Bioscan_Core.OnSyncStateChange))]
        private static void Post_CP_Bioscan_Core_OnSyncStateChanged(CP_Bioscan_Core __instance, 
            eBioscanStatus status, List<PlayerAgent> playersInScan)
        {
            bool IsConcurrentCluster = PlayerScannerManager.Current.IsConcurrentCluster(__instance);

            Logger.Debug($"IsConcurrentCluster: {IsConcurrentCluster}, IsMovable: {__instance.IsMovable}");

            if (status != eBioscanStatus.Scanning)
            {
                if (IsConcurrentCluster)
                {
                    PlayerScannerManager.Current.ConcurrentClusterShouldProgress(__instance, false);
                }

                return;
            }

            // We only handle Movable or Concurrent cluster in this method
            if (!__instance.IsMovable && !IsConcurrentCluster) return;

            bool ScanShouldProgress = true;
            CP_PlayerScanner scanner = PlayerScannerManager.Current.GetCacheScanner(__instance);

            if (scanner == null)
            {
                Logger.Error("Null CP_PlayerScanner");
                return;
            }

            // =========== (original) scan speed examination =========== 
            int playersInScanCount = playersInScan.Count;
            var playerAgentsInLevel = PlayerManager.PlayerAgentsInLevel;

            float scanSpeed = 0.0f;
            if (__instance.m_playerScanner.ScanPlayersRequired == PlayerRequirement.None)
            {
                if(IsConcurrentCluster)
                {
                    var originalScanSpeeds = PlayerScannerManager.Current.GetOriginalScanSpeed(__instance);
                    scanSpeed = playersInScanCount <= 0 ? 0.0f : originalScanSpeeds[playersInScanCount - 1];
                }
                else
                {
                    scanSpeed = playersInScanCount <= 0 ? 0.0f : scanner.m_scanSpeeds[playersInScanCount - 1];
                }
            }

            else if (scanner.m_playerRequirement == PlayerRequirement.All && playersInScanCount == playerAgentsInLevel.Count
                || (scanner.m_playerRequirement == PlayerRequirement.Solo && playersInScanCount == 1))
            {
                if (IsConcurrentCluster)
                {
                    var originalScanSpeeds = PlayerScannerManager.Current.GetOriginalScanSpeed(__instance);
                    scanSpeed = originalScanSpeeds[0];
                }
                else
                {
                    scanSpeed = scanner.m_scanSpeeds[playersInScanCount - 1];
                }
            }

            bool hasPositiveScanSpeed = scanSpeed > 0.0f;
            ScanShouldProgress = hasPositiveScanSpeed;
            if (hasPositiveScanSpeed)
            {
                // examine req item (for both concurrent cluster and T-scan)
                // I wonder if req item examination is required for concurrent cluster... 
                // but anyway examination is done on both now.
                if (scanner.m_reqItemsEnabled)
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
            }

            // Handle Concurrent cluster only when ScanShouldProgress == true for this scan
            if (IsConcurrentCluster)
            {
                if(ScanShouldProgress)
                {
                    ScanShouldProgress = PlayerScannerManager.Current.ConcurrentClusterShouldProgress(__instance, true);
                }
                else
                {
                    PlayerScannerManager.Current.ConcurrentClusterShouldProgress(__instance, false);
                }
            }

            if (ScanShouldProgress)
            {
                if(IsConcurrentCluster)
                {
                    var originalScanSpeeds = PlayerScannerManager.Current.GetOriginalScanSpeed(__instance);
                    for(int i = 0; i < 4; i++)
                    {
                        scanner.m_scanSpeeds[i] = originalScanSpeeds[i];
                    }
                }

                if (__instance.IsMovable)
                {
                    __instance.m_movingComp.ResumeMovement();
                }
            }
            else
            {
                if (IsConcurrentCluster)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        scanner.m_scanSpeeds[i] = 0.0f;
                    }
                }

                if (__instance.IsMovable)
                {
                    __instance.m_movingComp.PauseMovement();
                }
            }
        }

        static Patch_CP_Bioscan_Core_OnSyncStateChange()
        {

        }
    }
}
