﻿global using BasePlugin = BepInEx.Unity.IL2CPP.BasePlugin;
global using HarmonyX = HarmonyLib.Harmony;
global using Patch = HarmonyLib.HarmonyPatch;
global using Prefix = HarmonyLib.HarmonyPrefix;
global using Postfix = HarmonyLib.HarmonyPostfix;
global using ChainedPuzzle = ChainedPuzzles.ChainedPuzzleInstance;
global using ChainedPuzzleManager = ChainedPuzzles.ChainedPuzzleManager;
global using LogSource = BepInEx.Logging.ManualLogSource;
global using Area = LevelGeneration.LG_Area;
global using Vector3 = UnityEngine.Vector3;
global using Transform = UnityEngine.Transform;
global using PluginAttributes = BepInEx.BepInPlugin;
global using ChainedPuzzleDataBlock = GameData.ChainedPuzzleDataBlock;
global using Bioscan = ChainedPuzzles.CP_Bioscan_Core;
global using Clusterscan = ChainedPuzzles.CP_Cluster_Core;
global using GameObject = UnityEngine.GameObject;
global using Global = Globals.Global;
global using EventAPI = GTFO.API.EventAPI;
global using GameEventLog = PUI_GameEventLog;
global using Bioscans = Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<ChainedPuzzles.CP_Bioscan_Core>;
global using Clusterscans = Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase<ChainedPuzzles.CP_Cluster_Core>;
global using JsonSerializer = System.Text.Json.JsonSerializer;
global using JsonSerializerOptions = System.Text.Json.JsonSerializerOptions;
//global using JsonSerializer = GTFO.API.JSON.JsonSerializer;
global using BepInExPaths = BepInEx.Paths;
global using Quaternion = UnityEngine.Quaternion;