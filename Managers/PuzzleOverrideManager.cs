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

        // core -> info
        private Dictionary<CP_Bioscan_Core, uint> bioscanCore2Index = new();
        private Dictionary<CP_Cluster_Core, uint> clusterCore2Index = new();

        private Dictionary<uint, CP_Bioscan_Core> index2BioscanCore = new();
        private Dictionary<uint, CP_Cluster_Core> index2ClusterCore = new();


        // casting between iChainedPuzzleCore and CP_Bioscan_Core, CP_Cluster_Core is too painful.
        // so make life easier.
        private Dictionary<IntPtr, uint> bioscanCoreIntPtr2Index = new();
        private Dictionary<IntPtr, uint> clusterCoreIntPtr2Index = new();

        private uint puzzleOverrideIndex = 1u;

        public uint Register(CP_Bioscan_Core __instance)
        {
            if (__instance == null) return 0u;

            uint allotedIndex = puzzleOverrideIndex;
            puzzleOverrideIndex += 1;
            if (!bioscanCore2Index.ContainsKey(__instance))
            {
                bioscanCore2Index.Add(__instance, allotedIndex);
                bioscanCoreIntPtr2Index.Add(__instance.Pointer, allotedIndex);
                index2BioscanCore.Add(allotedIndex, __instance);
            }
            else
            {
                Logger.Error("Duplicate CP_Bioscan_Core registration, exm?");
                return 0u;
            }

            return allotedIndex;
        }

        public uint Register(CP_Cluster_Core __instance)
        {
            if (__instance == null) return 0u;

            uint allotedIndex = puzzleOverrideIndex;
            puzzleOverrideIndex += 1;
            if (!clusterCore2Index.ContainsKey(__instance))
            {
                clusterCore2Index.Add(__instance, allotedIndex);
                clusterCoreIntPtr2Index.Add(__instance.Pointer, allotedIndex);
                index2ClusterCore.Add(allotedIndex, __instance);
            }
            else
            {
                Logger.Error("Duplicate CP_Cluster_Core registration, exm?");
                return 0u;
            }

            return allotedIndex;
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
                    if (bioscanCoreIntPtr2Index.ContainsKey(core.Pointer))
                    {
                        uint puzzleOverrideIndex = bioscanCoreIntPtr2Index[core.Pointer];
                        chainedPuzzlesInfo.Append($"puzzle index: {i}\n");
                        chainedPuzzlesInfo.Append("type: CP_Bioscan_Core\n");
                        chainedPuzzlesInfo.Append($"PuzzleOverrideIndex: {puzzleOverrideIndex}\n");
                    }

                    // CP_Cluster_Core
                    else if (clusterCoreIntPtr2Index.ContainsKey(core.Pointer))
                    {
                        uint clusterCoreOverrideIndex = clusterCoreIntPtr2Index[core.Pointer];
                        CP_Cluster_Core clusterCore = core.TryCast<CP_Cluster_Core>();
                        if (clusterCore == null)
                        {
                            Logger.Error("Found cluster core Pointer, but TryCast failed.");
                            continue;
                        }

                        chainedPuzzlesInfo.Append($"puzzle index: {i}\n");
                        chainedPuzzlesInfo.Append("type: CP_Cluster_Core\n");
                        chainedPuzzlesInfo.Append($"PuzzleOverrideIndex: {clusterCoreOverrideIndex}\n");
                        chainedPuzzlesInfo.Append("=== Clustered puzzles info: ===\n");
                        for (int j = 0; j < clusterCore.m_amountOfPuzzles; j++)
                        {
                            iChainedPuzzleCore clusterChildCore = clusterCore.m_childCores[j];
                            if (!bioscanCoreIntPtr2Index.ContainsKey(clusterChildCore.Pointer))
                            {
                                Logger.Error("Unregistered clustered iChainedPuzzleCore found...");
                                continue;
                            }

                            uint puzzleOverrideIndex = bioscanCoreIntPtr2Index[clusterChildCore.Pointer];
                            chainedPuzzlesInfo.Append($"puzzle index: {j}\n");
                            chainedPuzzlesInfo.Append("type: CP_Bioscan_Core\n");
                            chainedPuzzlesInfo.Append($"PuzzleOverrideIndex: {puzzleOverrideIndex}\n");
                        }
                        chainedPuzzlesInfo.Append("=== Clustered puzzles END ===\n");
                    }
                    else
                    {
                        Logger.Error("Unregistered iChainedPuzzleCore found...");
                    }

                }
                chainedPuzzlesInfo.Append('\n');
            }

            Logger.Debug(chainedPuzzlesInfo.ToString());
        }

        public uint GetBioscanCoreOverrideIndex(CP_Bioscan_Core core) => !bioscanCore2Index.ContainsKey(core) ? 0u : bioscanCore2Index[core];

        public uint GetClusterCoreOverrideIndex(CP_Cluster_Core core) => !clusterCore2Index.ContainsKey(core) ? 0u : clusterCore2Index[core];

        public uint GetBioscanCoreOverrideIndex(IntPtr pointer) => !bioscanCoreIntPtr2Index.ContainsKey(pointer) ? 0u : bioscanCoreIntPtr2Index[pointer];

        public uint GetClusterCoreOverrideIndex(IntPtr pointer) => !clusterCoreIntPtr2Index.ContainsKey(pointer) ? 0u : clusterCoreIntPtr2Index[pointer];

        public CP_Bioscan_Core GetBioscanCore(uint puzzleOverrideIndex) => index2BioscanCore.ContainsKey(puzzleOverrideIndex) ? index2BioscanCore[puzzleOverrideIndex] : null;
        
        public CP_Cluster_Core GetClusterCore(uint puzzleOverrideIndex) => index2ClusterCore.ContainsKey(puzzleOverrideIndex) ? index2ClusterCore[puzzleOverrideIndex] : null;

        public void Clear()
        {
            puzzleOverrideIndex = 1u;
            bioscanCore2Index.Clear();
            clusterCore2Index.Clear();
            bioscanCoreIntPtr2Index.Clear();
            clusterCoreIntPtr2Index.Clear();
            index2BioscanCore.Clear();
            index2ClusterCore.Clear();
            Logger.Warning("Cleared PuzzleOverrideManager");
        }

        private PuzzleOverrideManager() { }

        static PuzzleOverrideManager()
        {
            Current = new();
            LevelAPI.OnEnterLevel += Current.OutputLevelPuzzleInfo;
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
                Logger.Error("Failed to find CP_BioScan_Core owner (instance of ChainedPuzzleInstance).");
                return null;
            }
        }
    }
}
