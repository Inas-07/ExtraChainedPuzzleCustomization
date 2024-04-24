using ChainedPuzzles;
using HarmonyLib;
using System;
using UnityEngine;

namespace ScanPosOverride.Patches
{

    [HarmonyPatch]
    internal static class Patch_CP_BasicMovable_TryGetNextIndex
    {
        /*
Setting breakpad minidump AppID = 493520
SteamInternal_SetMinidumpSteamID:  Caching Steam ID:  76561198242241002 [API loaded no]
Stack overflow.
   at Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(IntPtr, IntPtr, Void**, IntPtr ByRef)
   at Il2CppSystem.Type.GetMethod(System.String)
   at Il2CppInterop.Runtime.DelegateSupport.ConvertDelegate[[System.__Canon, System.Private.CoreLib, Version=6.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e]](System.Delegate)
   at ScanPosOverride.Patches.Patch_CP_BasicMovable_TryGetNextIndex.Pre_UpdateMovementRoutine(ChainedPuzzles.CP_BasicMovable)
   at DynamicClass.DMD<ChainedPuzzles.CP_BasicMovable::UpdateMovementRoutine>(ChainedPuzzles.CP_BasicMovable)
   at DynamicClass.(il2cpp -> managed) UpdateMovementRoutine(il2cpp -> managed) UpdateMovementRoutine(IntPtr, Il2CppInterop.Runtime.Runtime.Il2CppMethodInfo*)
   at Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(IntPtr, IntPtr, Void**, IntPtr ByRef)
   at Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(IntPtr, IntPtr, Void**, IntPtr ByRef)
   at ChainedPuzzles.CP_BasicMovable.UpdateMovementRoutine()
   at DynamicClass.(il2cpp delegate trampoline) System.Void_System.Action(il2cpp delegate trampoline) System.Void_System.Action(IntPtr, Il2CppInterop.Runtime.Runtime.Il2CppMethodInfo*)
   at Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(IntPtr, IntPtr, Void**, IntPtr ByRef)
   at Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(IntPtr, IntPtr, Void**, IntPtr ByRef)
   at UnityEngine.MonoBehaviour.StartCoroutine(Il2CppSystem.Collections.IEnumerator)
   at ScanPosOverride.Patches.Patch_CP_BasicMovable_TryGetNextIndex.Pre_UpdateMovementRoutine(ChainedPuzzles.CP_BasicMovable)
   at DynamicClass.DMD<ChainedPuzzles.CP_BasicMovable::UpdateMovementRoutine>(ChainedPuzzles.CP_BasicMovable)
   at DynamicClass.(il2cpp -> managed) UpdateMovementRoutine(il2cpp -> managed) UpdateMovementRoutine(IntPtr, Il2CppInterop.Runtime.Runtime.Il2CppMethodInfo*)
   at Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(IntPtr, IntPtr, Void**, IntPtr ByRef)
   at Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(IntPtr, IntPtr, Void**, IntPtr ByRef)
   at ChainedPuzzles.CP_BasicMovable.UpdateMovementRoutine()
         */
        //private static bool BetterTryGetNextIndex(this CP_BasicMovable movable, out int currentIndex, out int nextIndex)
        //{
        //    currentIndex = Math.Max((int)movable.LerpAmount, 0);
        //    int AmountOfPositions = movable.ScanPositions.Count;

        //    currentIndex %= AmountOfPositions;

        //    switch (movable.m_typeOfMovement)
        //    {
        //        case MovementType.None:
        //            nextIndex = -1;
        //            return false;

        //        case MovementType.Directional:
        //            if (currentIndex < AmountOfPositions - 1)
        //            {
        //                nextIndex = currentIndex + 1;
        //                return true;
        //            }
        //            else
        //            {
        //                nextIndex = -1;
        //                return false;
        //            }

        //        case MovementType.Circular:
        //            nextIndex = currentIndex + 1;
        //            nextIndex %= AmountOfPositions;
        //            if (movable.LerpAmount > AmountOfPositions)
        //            {
        //                movable.LerpAmount -= (int)movable.LerpAmount;
        //            }
        //            return true;

        //        default:
        //            nextIndex = -1;
        //            return false;
        //    }
        //}

        //[HarmonyPrefix]
        //[HarmonyPatch(typeof(CP_BasicMovable), nameof(CP_BasicMovable.ResetPosition))]
        //private static bool Pre_ResetPosition(CP_BasicMovable __instance)
        //{
        //    if (__instance.BetterTryGetNextIndex(out var index, out var index2))
        //    {
        //        float t = __instance.LerpAmount % 1f;
        //        __instance.transform.position = Vector3.Lerp(
        //            __instance.ScanPositions[index], 
        //            __instance.ScanPositions[index2], 
        //            t);
        //    }
        //    return false;
        //}

        //[HarmonyPrefix]
        //[HarmonyPatch(typeof(CP_BasicMovable), nameof(CP_BasicMovable.UpdateMovementRoutine))]
        //private static bool Pre_UpdateMovementRoutine(CP_BasicMovable __instance)
        //{
        //    __instance.m_reset = false;
        //    if (__instance.BetterTryGetNextIndex(out var index, out var index2))
        //    {
        //        if (__instance.m_moveCoroutine != null)
        //        {
        //            __instance.StopCoroutine(__instance.m_moveCoroutine);
        //        }
        //        __instance.m_moveCoroutine = __instance.StartCoroutine(
        //            __instance.DoMoveScanner(
        //                __instance.ScanPositions[index], 
        //                __instance.ScanPositions[index2], 
        //                new System.Action(__instance.UpdateMovementRoutine)));
        //    }

        //    return false;
        //}

        //[HarmonyPostfix]
        //[HarmonyPatch(typeof(CP_BasicMovable), nameof(CP_BasicMovable.TryGetNextIndex))]
        //private static void Post_TryGetNextIndex(CP_BasicMovable __instance, 
        //    ref int currentIndex, ref int nextIndex, ref bool __result)
        //    // TODO: harmony cannot patch method like this
        //{
        //    int AmountOfPositions = __instance.ScanPositions.Count;

        //    switch (__instance.m_typeOfMovement)
        //    {
        //        case MovementType.None:
        //            return;

        //        case MovementType.Directional:
        //            currentIndex = Math.Min(AmountOfPositions, currentIndex);
        //            if(currentIndex < AmountOfPositions - 1) 
        //            { 
        //                nextIndex = currentIndex + 1;
        //                __result = true;
        //            }
        //            else
        //            {
        //                nextIndex = -1;
        //                __result = false;
        //            }
        //            break;

        //        case MovementType.Circular:
        //            nextIndex = currentIndex + 1;
        //            currentIndex %= AmountOfPositions;
        //            nextIndex %= AmountOfPositions;

        //            __result = true;
        //            break;
        //    }
        //}
    }
}
