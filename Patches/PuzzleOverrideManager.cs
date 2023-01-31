using ChainedPuzzles;
using System;
using System.Collections.Generic;
using System.Text;
using GTFO.API;
using LevelGeneration;
using ScanPosOverride.PuzzleOverrideData;
using System.Linq;

namespace ScanPosOverride.Patches
{
    internal class PuzzleOverrideManager
    {
        public static uint MainLevelLayout => RundownManager.ActiveExpedition.LevelLayoutData;

        public static readonly PuzzleOverrideManager Current;

        // core -> info
        private Dictionary<CP_Bioscan_Core, uint> bioscanCoreInfo = new();
        private Dictionary<CP_Cluster_Core, uint> clusterCoreInfo = new();

        // casting between iChainedPuzzleCore and CP_Bioscan_Core, CP_Cluster_Core is too painful.
        // so make life easier.
        private Dictionary<IntPtr, uint> bioscanCoreInfo_IntPtr = new();
        private Dictionary<IntPtr, uint> clusterCoreInfo_IntPtr = new();

        private uint puzzleOverrideIndex = 1u;

        public uint register(CP_Bioscan_Core __instance)
        {
            if (__instance == null) return 0u;

            uint allotedIndex = puzzleOverrideIndex;
            puzzleOverrideIndex += 1;
            if (!bioscanCoreInfo.ContainsKey(__instance))
            {
                bioscanCoreInfo.Add(__instance, allotedIndex);
                bioscanCoreInfo_IntPtr.Add(__instance.Pointer, allotedIndex);
            }
            else
            {
                Logger.Error("Duplicate CP_Bioscan_Core registration, exm?");
                return 0u;
            }

            return allotedIndex;
        }

        public uint register(CP_Cluster_Core __instance)
        {
            if (__instance == null) return 0u;

            uint allotedIndex = puzzleOverrideIndex;
            puzzleOverrideIndex += 1;
            if (!clusterCoreInfo.ContainsKey(__instance))
            {
                clusterCoreInfo.Add(__instance, allotedIndex);
                clusterCoreInfo_IntPtr.Add(__instance.Pointer, allotedIndex);  
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
        public void OnBuildDone_OutputLevelPuzzleInfo()
        {
            List<ChainedPuzzleInstance> levelChainedPuzzleInstances = new();
            foreach(var cpInstance in ChainedPuzzleManager.Current.m_instances)
                levelChainedPuzzleInstances.Add(cpInstance);

            levelChainedPuzzleInstances.Sort((c1, c2) => {
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
                chainedPuzzlesInfo.Append($"\nZone {srcZone.Alias}, Layer {srcZone.m_layer.m_type}, Dim {srcZone.DimensionIndex}\n");
                chainedPuzzlesInfo.Append($"Alarm name: {chainedPuzzleInstance.Data.PublicAlarmName}:\n");
                chainedPuzzlesInfo.Append("----");

                // could be either CP_Bioscan_Core or CP_Cluster_Core
                for(int i = 0; i < chainedPuzzleInstance.m_chainedPuzzleCores.Count; i++)
                {
                    iChainedPuzzleCore core = chainedPuzzleInstance.m_chainedPuzzleCores[i];

                    // CP_Bioscan_Core
                    if (bioscanCoreInfo_IntPtr.ContainsKey(core.Pointer))
                    {
                        uint puzzleOverrideIndex = bioscanCoreInfo_IntPtr[core.Pointer];
                        chainedPuzzlesInfo.Append($"puzzle index: {i}\n");
                        chainedPuzzlesInfo.Append("type: CP_Bioscan_Core\n");
                        chainedPuzzlesInfo.Append($"PuzzleOverrideIndex: {puzzleOverrideIndex}\n");
                    }

                    // CP_Cluster_Core
                    else if (clusterCoreInfo_IntPtr.ContainsKey(core.Pointer))
                    {
                        uint clusterCoreOverrideIndex = clusterCoreInfo_IntPtr[core.Pointer];
                        CP_Cluster_Core clusterCore = core.TryCast<CP_Cluster_Core>();
                        if(clusterCore == null)
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
                            if (!bioscanCoreInfo_IntPtr.ContainsKey(clusterChildCore.Pointer))
                            {
                                Logger.Error("Unregistered clustered iChainedPuzzleCore found...");
                                continue;
                            }

                            uint puzzleOverrideIndex = bioscanCoreInfo_IntPtr[clusterChildCore.Pointer];
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

        public uint GetBioscanCoreOverrideIndex(CP_Bioscan_Core core) => !bioscanCoreInfo.ContainsKey(core) ? 0u : bioscanCoreInfo[core];

        public uint GetClusterCoreOverrideIndex(CP_Cluster_Core core) => !clusterCoreInfo.ContainsKey(core) ? 0u : clusterCoreInfo[core];

        public uint GetBioscanCoreOverrideIndex(IntPtr pointer) => !bioscanCoreInfo_IntPtr.ContainsKey(pointer) ? 0u : bioscanCoreInfo_IntPtr[pointer];

        public uint GetClusterCoreOverrideIndex(IntPtr pointer) => !clusterCoreInfo_IntPtr.ContainsKey(pointer) ? 0u : clusterCoreInfo_IntPtr[pointer];

        public void Clear()
        {
            puzzleOverrideIndex = 1u;
            bioscanCoreInfo.Clear();
            clusterCoreInfo.Clear();
            bioscanCoreInfo_IntPtr.Clear();
            clusterCoreInfo_IntPtr.Clear();
            Logger.Warning("Cleared PuzzleOverrideManager");
        }

        private PuzzleOverrideManager() { }

        static PuzzleOverrideManager()
        {
            Current = new();
            LevelAPI.OnBuildDone += Current.OnBuildDone_OutputLevelPuzzleInfo;
            LevelAPI.OnLevelCleanup += Current.Clear;
        }

        // find iChainedPuzzleOwner, which can be casted to `ChainedPuzzleInstance`.
        private iChainedPuzzleOwner ChainedPuzzleInstanceOwner(CP_Bioscan_Core bioscanCore)
        {
            if (bioscanCore == null) return null;

            iChainedPuzzleOwner owner = bioscanCore.Owner;

            ChainedPuzzleInstance cpInstance = owner.TryCast<ChainedPuzzleInstance>();
            if(cpInstance != null) return owner;

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
