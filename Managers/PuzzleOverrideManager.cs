using ChainedPuzzles;
using System;
using System.Collections.Generic;
using System.Text;
using GTFO.API;
using LevelGeneration;

namespace ScanPosOverride.Managers
{
    public class PuzzleOverrideManager
    {
        public static uint MainLevelLayout => RundownManager.ActiveExpedition.LevelLayoutData;

        public static readonly PuzzleOverrideManager Current;

        // key: CP_Bioscan_Core.Pointer, value: override index
        private Dictionary<IntPtr, uint> bioscanIndex { get; } = new();

        // key: CP_Cluster_Core.Pointer, value: override index
        private Dictionary<IntPtr, uint> clusterIndex { get; } = new();

        private Dictionary<uint, CP_Bioscan_Core> index2Bioscan { get; } = new();

        private Dictionary<uint, CP_Cluster_Core> index2Cluster { get; } = new();

        private uint puzzleOverrideIndex = 1u;

        public uint Register(CP_Bioscan_Core __instance)
        {
            if (__instance == null) return 0u;

            uint allotedIndex = puzzleOverrideIndex;
            puzzleOverrideIndex += 1;
            if (!bioscanIndex.ContainsKey(__instance.Pointer))
            {
                bioscanIndex.Add(__instance.Pointer, allotedIndex);
                //bioscanCoreIntPtr2Index.Add(__instance.Pointer, allotedIndex);
                index2Bioscan.Add(allotedIndex, __instance);
                return allotedIndex;
            }
            else
            {
                return GetBioscanCoreOverrideIndex(__instance);
            }
        }

        public uint Register(CP_Cluster_Core __instance)
        {
            if (__instance == null) return 0u;

            uint allotedIndex = puzzleOverrideIndex;
            puzzleOverrideIndex += 1;
            if (!clusterIndex.ContainsKey(__instance.Pointer))
            {
                clusterIndex.Add(__instance.Pointer, allotedIndex);
                //clusterCoreIntPtr2Index.Add(__instance.Pointer, allotedIndex);
                index2Cluster.Add(allotedIndex, __instance);
               return allotedIndex;
            }
            else
            {
                return GetClusterCoreOverrideIndex(__instance);
            }
        }

        // output by chained puzzle instance.
        // ordered by DimensionIndex, Layer, LocalIndex, PuzzleOverrideIndex.
        // For each ChainedPuzzleInstance: firstly output info of CP_Bioscan_Core, then CP_Cluster_Core
        public void OutputLevelPuzzleInfo()
        {
            List<ChainedPuzzleInstance> levelChainedPuzzleInstances = new();
            foreach (var cpInstance in ChainedPuzzleManager.Current.m_instances)
                levelChainedPuzzleInstances.Add(cpInstance);

            levelChainedPuzzleInstances.Sort((c1, c2) =>
            {
                LG_Zone z1 = c1.m_sourceArea.m_zone;
                LG_Zone z2 = c2.m_sourceArea.m_zone;
                if (z1.DimensionIndex != z2.DimensionIndex) return (uint)z1.DimensionIndex < (uint)z2.DimensionIndex ? -1 : 1;
                if (z1.Layer.m_type != z2.Layer.m_type) return (uint)z1.Layer.m_type < (uint)z2.Layer.m_type ? -1 : 1;

                return (uint)z1.LocalIndex < (uint)z2.LocalIndex ? -1 : 1;
            });

            StringBuilder chainedPuzzlesInfo = new();
            foreach (var chainedPuzzleInstance in levelChainedPuzzleInstances)
            {

                LG_Zone srcZone = chainedPuzzleInstance.m_sourceArea.m_zone;
                // alarm info. 
                chainedPuzzlesInfo.Append($"\nZone {srcZone.Alias}, {srcZone.m_layer.m_type}, Dim {srcZone.DimensionIndex}\n");
                chainedPuzzlesInfo.Append($"Alarm name: {chainedPuzzleInstance.Data.PublicAlarmName}:\n");

                // could be either CP_Bioscan_Core or CP_Cluster_Core
                for (int i = 0; i < chainedPuzzleInstance.m_chainedPuzzleCores.Count; i++)
                {
                    iChainedPuzzleCore core = chainedPuzzleInstance.m_chainedPuzzleCores[i];

                    // CP_Bioscan_Core
                    
                    if (bioscanIndex.ContainsKey(core.Pointer))
                    //if (bioscanCoreIntPtr2Index.ContainsKey(core.Pointer))
                    {
                        uint puzzleOverrideIndex = bioscanIndex[core.Pointer];
                        chainedPuzzlesInfo.Append($"puzzle index: {i}\n");
                        chainedPuzzlesInfo.Append("type: CP_Bioscan_Core\n");
                        chainedPuzzlesInfo.Append($"PuzzleOverrideIndex: {puzzleOverrideIndex}\n");

                        var bioscanCore = core.Cast<CP_Bioscan_Core>();
                        var scanner = bioscanCore.PlayerScanner.Cast<CP_PlayerScanner>();
                        chainedPuzzlesInfo.Append($"Position: {bioscanCore.m_position}\n");
                        chainedPuzzlesInfo.Append($"Radius: {scanner.Radius}\n");
                    }

                    // CP_Cluster_Core
                    else if (clusterIndex.ContainsKey(core.Pointer))
                    {
                        uint clusterCoreOverrideIndex = clusterIndex[core.Pointer];
                        CP_Cluster_Core clusterCore = core.TryCast<CP_Cluster_Core>();
                        if (clusterCore == null)
                        {
                            SPOLogger.Error("Found cluster core Pointer, but TryCast failed.");
                            continue;
                        }

                        chainedPuzzlesInfo.Append($"puzzle index: {i}\n");
                        chainedPuzzlesInfo.Append("type: CP_Cluster_Core\n");
                        chainedPuzzlesInfo.Append($"PuzzleOverrideIndex: {clusterCoreOverrideIndex}\n");
                        chainedPuzzlesInfo.Append("=== Clustered puzzles info: ===\n");
                        for (int j = 0; j < clusterCore.m_amountOfPuzzles; j++)
                        {
                            iChainedPuzzleCore clusterChildCore = clusterCore.m_childCores[j];
                            if (!bioscanIndex.ContainsKey(clusterChildCore.Pointer))
                            {
                                SPOLogger.Error("Unregistered clustered iChainedPuzzleCore found...");
                                continue;
                            }

                            uint puzzleOverrideIndex = bioscanIndex[clusterChildCore.Pointer];
                            chainedPuzzlesInfo.Append($"puzzle index: {j}\n");
                            chainedPuzzlesInfo.Append("type: CP_Bioscan_Core\n");
                            chainedPuzzlesInfo.Append($"PuzzleOverrideIndex: {puzzleOverrideIndex}\n");
                            
                            var bioscanCore = clusterChildCore.Cast<CP_Bioscan_Core>();
                            var scanner = bioscanCore.PlayerScanner.Cast<CP_PlayerScanner>();
                            chainedPuzzlesInfo.Append($"Position: {bioscanCore.m_position}\n");
                            chainedPuzzlesInfo.Append($"Radius: {scanner.Radius}\n");
                        }
                        chainedPuzzlesInfo.Append("=== Clustered puzzles END ===\n");
                    }
                    else
                    {
                        SPOLogger.Error("Unregistered iChainedPuzzleCore found...");
                    }

                }
                chainedPuzzlesInfo.Append('\n');
            }

            SPOLogger.Debug(chainedPuzzlesInfo.ToString());
        }

        public uint GetBioscanCoreOverrideIndex(CP_Bioscan_Core core) => !bioscanIndex.ContainsKey(core.Pointer) ? 0u : bioscanIndex[core.Pointer];

        public uint GetClusterCoreOverrideIndex(CP_Cluster_Core core) => !clusterIndex.ContainsKey(core.Pointer) ? 0u : clusterIndex[core.Pointer];

        public uint GetBioscanCoreOverrideIndex(IntPtr pointer) => !bioscanIndex.ContainsKey(pointer) ? 0u : bioscanIndex[pointer];

        public uint GetClusterCoreOverrideIndex(IntPtr pointer) => !clusterIndex.ContainsKey(pointer) ? 0u : clusterIndex[pointer];

        public CP_Bioscan_Core GetBioscanCore(uint puzzleOverrideIndex) => index2Bioscan.ContainsKey(puzzleOverrideIndex) ? index2Bioscan[puzzleOverrideIndex] : null;
        
        public CP_Cluster_Core GetClusterCore(uint puzzleOverrideIndex) => index2Cluster.ContainsKey(puzzleOverrideIndex) ? index2Cluster[puzzleOverrideIndex] : null;

        public void Clear()
        {
            puzzleOverrideIndex = 1u;
            bioscanIndex.Clear();
            clusterIndex.Clear();
            index2Bioscan.Clear();
            index2Cluster.Clear();
        }

        private PuzzleOverrideManager() { }

        static PuzzleOverrideManager()
        {
            Current = new();
            LevelAPI.OnEnterLevel += Current.OutputLevelPuzzleInfo;
            LevelAPI.OnBuildStart += Current.Clear;
            LevelAPI.OnLevelCleanup += Current.Clear;
        }

        /** 
         * Summary:
         *      Find iChainedPuzzleOwner, which can be casted to `ChainedPuzzleInstance`.
        */
        public iChainedPuzzleOwner ChainedPuzzleInstanceOwner(CP_Bioscan_Core bioscanCore)
        {
            if (bioscanCore == null) return null;

            iChainedPuzzleOwner owner = bioscanCore.Owner;

            ChainedPuzzleInstance cpInstance = owner.TryCast<ChainedPuzzleInstance>();
            if (cpInstance != null) return owner;

            CP_Cluster_Core clusterOwner = owner.TryCast<CP_Cluster_Core>();
            if (clusterOwner != null)
            {
                owner = clusterOwner.m_owner;
                return owner;
            }
            else
            {
                SPOLogger.Error("Failed to find CP_BioScan_Core owner (instance of ChainedPuzzleInstance).");
                return null;
            }
        }
    }
}
