using ChainedPuzzles;
using System.Collections.Generic;
using HarmonyLib;
using LevelGeneration;
using ScanPosOverride.Managers;
using UnityEngine;
using Player;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using GTFO.API;

namespace ScanPosOverride.Patches
{
    [HarmonyPatch]
    internal class Patch_CP_PlayerScanner
    {
        // PlayerScanner -> Wrapper
        private static Dictionary<System.IntPtr, Wrapper> movableScanWithReqItem = new();

        private class Wrapper
        {
            private CP_PlayerScanner scanner;
            private CP_Bioscan_Core core;
            
            private Coroutine UpdateMovableScanCoroutine = null;

            internal Wrapper(CP_PlayerScanner scanner, CP_Bioscan_Core core)
            {
                this.scanner = scanner;
                this.core = core;
            }

            private System.Collections.IEnumerator UpdateMovableScan()
            {
                while(true)
                {
                    int playersInScanCount = 0;
                    var playerAgentsInLevel = PlayerManager.PlayerAgentsInLevel;
                    for (int index = 0; index < playerAgentsInLevel.Count; ++index)
                    {
                        if (playerAgentsInLevel[index] != null && playerAgentsInLevel[index].Alive)
                        {
                            Vector3 vector3_1 = scanner.transform.position - playerAgentsInLevel[index].Position;
                            if (vector3_1.sqrMagnitude < scanner.m_scanRadiusSqr)
                                playersInScanCount++;
                        }
                    }

                    float scanSpeed = 0.0f;
                    if (scanner.m_playerRequirement == PlayerRequirement.None)
                    {
                        scanSpeed = scanner.m_scanSpeeds[playersInScanCount];
                        //Logger.Warning("Requirement None satisfied");
                    }
                    else if (scanner.m_playerRequirement == PlayerRequirement.All && playersInScanCount == playerAgentsInLevel.Count
                        || (scanner.m_playerRequirement == PlayerRequirement.Solo && playersInScanCount == 1))
                    {
                        scanSpeed = scanner.m_scanSpeeds[0];
                    }

                    bool hasPositiveScanSpeed = scanSpeed > 0.0f;
                    if (hasPositiveScanSpeed)
                    {
                        bool ScanShouldProgress = true;

                        if (scanner.m_reqItemsEnabled)
                        {
                            for (int index = 0; index < scanner.m_reqItems.Length; ++index)
                            {
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
                            core.m_movingComp.ResumeMovement();
                        }
                        else
                        {
                            core.m_movingComp.PauseMovement();
                        }

                    }

                    yield return null;
                }
            }

            internal void StartScan()
            {
                if(UpdateMovableScanCoroutine == null)
                {
                    UpdateMovableScanCoroutine = scanner.StartCoroutine(UpdateMovableScan().WrapToIl2Cpp());
                }
            }

            internal void StopScan()
            {
                if (UpdateMovableScanCoroutine != null)
                {
                    scanner.StopCoroutine(UpdateMovableScanCoroutine);
                    UpdateMovableScanCoroutine = null;
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CP_PlayerScanner), nameof(CP_PlayerScanner.StartScan))]
        private static void Post_CP_PlayerScanner_StartScan(CP_PlayerScanner __instance)
        {
            // 1. Find CP_Bioscan_Core, get m_movingComp
            // 2. Only overwrite the method when has req item and is movable.
            // 3. (optional) impl `add / remove req item` event
            CP_Bioscan_Core core = PuzzleReqItemManager.Current.GetMovableCoreWithReqItem(__instance);
            if (core == null || !core.IsMovable || !core.m_reqItemsEnabled) return;

            Wrapper wrapper = null;
            if (!movableScanWithReqItem.ContainsKey(__instance.Pointer))
            {
                wrapper = new Wrapper(__instance, core);
                movableScanWithReqItem.Add(__instance.Pointer, wrapper);
            }
            else wrapper = movableScanWithReqItem[__instance.Pointer];

            wrapper.StartScan();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CP_PlayerScanner), nameof(CP_PlayerScanner.StopScan))]
        private static void Post_CP_PlayerScanner_StopScan(CP_PlayerScanner __instance)
        {

            if (!movableScanWithReqItem.ContainsKey(__instance.Pointer)) return;
            CP_Bioscan_Core core = PuzzleReqItemManager.Current.GetMovableCoreWithReqItem(__instance);

            Wrapper wrapper = movableScanWithReqItem[__instance.Pointer];

            wrapper.StopScan();

            movableScanWithReqItem.Remove(__instance.Pointer);
        }

        static Patch_CP_PlayerScanner()
        {
            foreach (var wrapper in movableScanWithReqItem.Values)
            {
                wrapper.StopScan();
            }

            LevelAPI.OnLevelCleanup += movableScanWithReqItem.Clear;
        }
    }
}
