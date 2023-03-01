﻿using ChainedPuzzles;
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
        // implementation of T-Scan moving policy && Concurrent cluster scan both fall into this method.
        // maintaining this patch could make you insane 
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CP_Bioscan_Core), nameof(CP_Bioscan_Core.OnSyncStateChange))]
        private static void Post_CP_Bioscan_Core_OnSyncStateChanged(CP_Bioscan_Core __instance, float progress,
            eBioscanStatus status, List<PlayerAgent> playersInScan)
        {
            bool IsConcurrentCluster = PlayerScannerManager.Current.IsConcurrentCluster(__instance);

            if (status != eBioscanStatus.Scanning)
            {
                if (IsConcurrentCluster)
                {
                    if(status == eBioscanStatus.Finished)
                    {
                        var clusterParent = PlayerScannerManager.Current.GetParentClusterCore(__instance);
                        if(clusterParent == null)
                        {
                            Logger.Error("Cannot find parent cluster core! The concurrent cluster may fail!");
                        }
                        else
                        {
                            PlayerScannerManager.Current.CompleteConcurrentCluster(clusterParent);
                        }
                    }
                    else
                    {
                        PlayerScannerManager.Current.ConcurrentClusterShouldProgress(__instance, false);
                    }
                }

                return;
            }

            if (!__instance.IsMovable && !IsConcurrentCluster) return;            
            
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
            float[] originalScanSpeeds = PlayerScannerManager.Current.GetCacheOriginalScanSpeed(__instance);
            if (__instance.m_playerScanner.ScanPlayersRequired == PlayerRequirement.None)
            {
                // handle concurrent cluster as well
                scanSpeed = playersInScanCount <= 0 ? 0.0f : originalScanSpeeds[playersInScanCount - 1];
            }

            else if (scanner.m_playerRequirement == PlayerRequirement.All && playersInScanCount == playerAgentsInLevel.Count
                || (scanner.m_playerRequirement == PlayerRequirement.Solo && playersInScanCount == 1))
            {
                scanSpeed = originalScanSpeeds[0];
            }

            bool ScanShouldProgress = scanSpeed > 0.0f;
            // req item check
            if (ScanShouldProgress)
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

            if (IsConcurrentCluster)
            {
                if(ScanShouldProgress)
                {
                    ScanShouldProgress = PlayerScannerManager.Current.ConcurrentClusterShouldProgress(__instance, true);
                }
                else // ScanShouldProgress == false
                {
                    PlayerScannerManager.Current.ConcurrentClusterShouldProgress(__instance, false);
                }
            }

            if (ScanShouldProgress)
            {
                if(IsConcurrentCluster)
                {
                    var clusterParent = PlayerScannerManager.Current.GetParentClusterCore(__instance);
                    if (clusterParent == null) Logger.Error("null clusterParent");

                    PlayerScannerManager.Current.RestoreConcurrentClusterScanSpeed(clusterParent);
                }

                if (__instance.IsMovable)
                {
                    __instance.m_movingComp.ResumeMovement();
                }

                if(scanner.m_playerRequirement == PlayerRequirement.None && scanner.m_reqItemsEnabled)
                {
                    scanner.m_scanSpeeds[0] = playersInScanCount > 0 ? originalScanSpeeds[playersInScanCount - 1] : 0.0f;
                }
            }
            else
            {
                if (IsConcurrentCluster)
                {
                    var clusterParent = PlayerScannerManager.Current.GetParentClusterCore(__instance);
                    if (clusterParent == null) Logger.Error("null clusterParent");

                    PlayerScannerManager.Current.ZeroConcurrentClusterScanSpeed(clusterParent);
                }

                if (__instance.IsMovable)
                {
                    __instance.m_movingComp.PauseMovement();
                }

                if (scanner.m_playerRequirement == PlayerRequirement.None && scanner.m_reqItemsEnabled)
                {
                    // cache original scan speed
                    // Concurrent Cluster scan speed is also handled in this method
                    PlayerScannerManager.Current.GetCacheOriginalScanSpeed(__instance);
                    scanner.m_scanSpeeds[0] = 0.0f;

                    Logger.Debug($"zeroed scan speed");
                }
            }
        }

        static Patch_CP_Bioscan_Core_OnSyncStateChange()
        {

        }
    }
}
