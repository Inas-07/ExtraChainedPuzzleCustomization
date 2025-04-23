using ChainedPuzzles;
using HarmonyLib;
using Player;
using LevelGeneration;
using UnityEngine;
using ScanPosOverride.Managers;
using GameData;
using System.Collections.Generic;
using Il2cppPlayerList = Il2CppSystem.Collections.Generic.List<Player.PlayerAgent>;
using GTFO.API;
using GTFO.API.Extensions;

namespace ScanPosOverride.Patches
{
    [HarmonyPatch]
    internal static class Patch_CP_Bioscan_Core_OnSyncStateChange
    {
        // implementation of:
        // 1. T-Scan moving policy
        // 2. Concurrent cluster scan 
        // 3. Events on bioscan progress
        // maintaining this patch could make you insane 
        private static Dictionary<System.IntPtr, int> EOPIndex = new();

        static Patch_CP_Bioscan_Core_OnSyncStateChange()
        {
            LevelAPI.OnBuildStart += EOPIndex.Clear;
            LevelAPI.OnLevelCleanup += EOPIndex.Clear;
        }

        // I'm tired of maintaining all shit in a single patch so I separated them
        [HarmonyPostfix]
        [HarmonyPatch(typeof(CP_Bioscan_Core), nameof(CP_Bioscan_Core.OnSyncStateChange))]
        private static void Post_OnSyncStateChanged_CheckEOPAndEventsOnPuzzleSolved(CP_Bioscan_Core __instance, float progress,
            eBioscanStatus status, Il2cppPlayerList playersInScan, bool isDropinState)
        {
            var overrideIndex = PuzzleOverrideManager.Current.GetBioscanCoreOverrideIndex(__instance);
            var def = Plugin.GetOverride(PuzzleOverrideManager.MainLevelLayout, overrideIndex);
            if (def == null) return;

            if (def.EventsOnBioscanProgress.Count > 0)
            {
                CheckBioscanEventsOnProgress();

                void CheckBioscanEventsOnProgress()
                {
                    if (!EOPIndex.ContainsKey(__instance.Pointer))
                    {
                        EOPIndex[__instance.Pointer] = 0;
                    }

                    int i = EOPIndex[__instance.Pointer];
                    if (isDropinState)
                    {
                        i = 0; // full reset to rerun events
                        while (i < def.EventsOnBioscanProgress.Count)
                        {
                            var curEOP = def.EventsOnBioscanProgress[i];
                            if (curEOP.Progress > progress)
                            {
                                break;
                            }
                            else
                            {
                                i++;
                            }
                        }
                        EOPIndex[__instance.Pointer] = i; // actually set the index to where it's supposed to be
                        return;
                    }

                    if (i < def.EventsOnBioscanProgress.Count)
                    {
                        var curEOP = def.EventsOnBioscanProgress[i];
                        if (curEOP.Progress < progress)
                        {
                            WardenObjectiveManager.CheckAndExecuteEventsOnTrigger(def.EventsOnBioscanProgress[i].Events.ToIl2Cpp(), eWardenObjectiveEventTrigger.None, true);
                            EOPIndex[__instance.Pointer] = i + 1;
                        }
                    }
                }
            }

            if (status == eBioscanStatus.Finished && !isDropinState && def.EventsOnPuzzleSolved.Count > 0)
            {
                WardenObjectiveManager.CheckAndExecuteEventsOnTrigger(def.EventsOnPuzzleSolved.ToIl2Cpp(), eWardenObjectiveEventTrigger.None, true);
            }
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(CP_Bioscan_Core), nameof(CP_Bioscan_Core.OnSyncStateChange))]
        private static void Post_OnSyncStateChanged_CheckReqItemAndConcurrentCluster(CP_Bioscan_Core __instance, float progress,
            eBioscanStatus status, Il2cppPlayerList playersInScan, bool isDropinState)
        {
            // handle reqItem and CP_Basic_Movable
            bool IsConcurrentCluster = PlayerScannerManager.Current.IsConcurrentCluster(__instance);
            // DEBUG:
            if(IsConcurrentCluster && __instance.m_reqItemsEnabled)
            {
                SPOLogger.Warning($"OnSyncStateChange: status - {status}, playersInScan - {playersInScan.Count}, progress: {progress}");
            }

            if (status != eBioscanStatus.Scanning)
            {
                if (IsConcurrentCluster)
                {
                    if(status == eBioscanStatus.Finished)
                    {
                        //var clusterParent = PlayerScannerManager.Current.GetParentClusterCore(__instance);
                        var parent = __instance.Owner.TryCast<CP_Cluster_Core>();

                        if (parent == null)
                        {
                            SPOLogger.Error("Cannot find parent cluster core! The concurrent cluster may fail!");
                        }
                        else
                        {
                            PlayerScannerManager.Current.CompleteConcurrentCluster(parent, __instance);
                        }
                    }
                    else
                    {
                        PlayerScannerManager.Current.CCShouldProgress(__instance, false);
                    }
                }

                if (__instance.m_reqItemsEnabled)
                {
                    __instance.m_graphics.SetColorMode(__instance.m_hasAlarm ? eChainedPuzzleGraphicsColorMode.Alarm_Waiting : eChainedPuzzleGraphicsColorMode.Waiting);
                }

                return;
            }

            if (!__instance.IsMovable && !IsConcurrentCluster && !__instance.m_reqItemsEnabled) return;            
            
            CP_PlayerScanner scanner = PlayerScannerManager.Current.GetCacheScanner(__instance);

            if (scanner == null)
            {
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
                    ScanShouldProgress = PlayerScannerManager.Current.CCShouldProgress(__instance, true);
                }
                else // ScanShouldProgress == false
                {
                    PlayerScannerManager.Current.CCShouldProgress(__instance, false);
                }
            }

            if (ScanShouldProgress)
            {
                if(IsConcurrentCluster)
                {
                    var parent = __instance.Owner.Cast<CP_Cluster_Core>();
                    PlayerScannerManager.Current.RestoreCCScanSpeed(parent);
                }

                if (__instance.IsMovable)
                {
                    __instance.m_movingComp.ResumeMovement();
                }

                if(scanner.m_reqItemsEnabled)
                {
                    if (scanner.m_playerRequirement == PlayerRequirement.None)
                    {
                        scanner.m_scanSpeeds[0] = playersInScanCount > 0 ? originalScanSpeeds[playersInScanCount - 1] : 0.0f;
                    }

                    __instance.m_graphics.SetColorMode(__instance.m_hasAlarm ? eChainedPuzzleGraphicsColorMode.Alarm_Active : eChainedPuzzleGraphicsColorMode.Active);
                }
            }
            else
            {
                if (IsConcurrentCluster)
                {
                    var parent = __instance.Owner.Cast<CP_Cluster_Core>();
                    PlayerScannerManager.Current.ZeroCCScanSpeed(parent);
                }

                if (__instance.IsMovable)
                {
                    __instance.m_movingComp.PauseMovement();
                }

                if(scanner.m_reqItemsEnabled)
                {
                    if (scanner.m_playerRequirement == PlayerRequirement.None)
                    {
                        // cache original scan speed
                        // Concurrent Cluster scan speed is also handled in this method
                        PlayerScannerManager.Current.GetCacheOriginalScanSpeed(__instance);
                        scanner.m_scanSpeeds[0] = 0.0f;
                    }

                    __instance.m_graphics.SetColorMode(__instance.m_hasAlarm ? eChainedPuzzleGraphicsColorMode.Alarm_Waiting : eChainedPuzzleGraphicsColorMode.Waiting);
                }
            }
        }

    }
}
