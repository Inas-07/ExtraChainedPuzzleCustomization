using ChainedPuzzles;
using System.Collections.Generic;
using System.Text;
using GTFO.API;
using LevelGeneration;
using ExtraObjectiveSetup.BaseClasses;
using GameData;
using AIGraph;
using System;

namespace ScanPosOverride.Managers
{
    public class PuzzleInstanceManager: InstanceManager<PuzzleWrapper>
    {
        public static uint MainLevelLayout => RundownManager.ActiveExpedition.LevelLayoutData;

        public static readonly PuzzleInstanceManager Current = new();

        public uint Register(CP_Bioscan_Core __instance, AIG_CourseNode courseNode) => base.Register(new PuzzleWrapper(__instance, courseNode));
        
        public uint Register(CP_Cluster_Core __instance, AIG_CourseNode courseNode) => base.Register(new PuzzleWrapper(__instance, courseNode));

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

                var globalZoneIdx = (srcZone.DimensionIndex, srcZone.Layer.m_type, srcZone.LocalIndex);
                var instancesInZone = base.instances2Index[globalZoneIdx];

                // could be either CP_Bioscan_Core or CP_Cluster_Core
                for (int i = 0; i < chainedPuzzleInstance.m_chainedPuzzleCores.Count; i++)
                {
                    iChainedPuzzleCore core = chainedPuzzleInstance.m_chainedPuzzleCores[i];

                    if (instancesInZone.ContainsKey(core.Pointer))
                    {
                        uint instanceIdx = instancesInZone[core.Pointer];
                        var wrapper = base.GetInstance(globalZoneIdx, instanceIdx);

                        // CP_Bioscan_Core
                        if (wrapper.bioscan_Core != null)
                        {
                            chainedPuzzlesInfo.Append($"puzzle index: {i}\n");
                            chainedPuzzlesInfo.Append("type: CP_Bioscan_Core\n");
                            chainedPuzzlesInfo.Append($"PuzzleOverrideIndex: {instanceIdx}\n");
                        }

                        // CP_Cluster_Core
                        else if (wrapper.cluster_Core != null)
                        {
                            CP_Cluster_Core clusterCore = core.Cast<CP_Cluster_Core>();

                            chainedPuzzlesInfo.Append($"puzzle index: {i}\n");
                            chainedPuzzlesInfo.Append("type: CP_Cluster_Core\n");
                            chainedPuzzlesInfo.Append($"PuzzleOverrideIndex: {instanceIdx}\n");
                            chainedPuzzlesInfo.Append("=== Clustered puzzles info: ===\n");
                            for (int j = 0; j < clusterCore.m_amountOfPuzzles; j++)
                            {
                                iChainedPuzzleCore clusterChildCore = clusterCore.m_childCores[j];
                                if (!instancesInZone.ContainsKey(clusterChildCore.Pointer))
                                {
                                    ScanPosOverrideLogger.Error("Unregistered clustered iChainedPuzzleCore found...");
                                    continue;
                                }

                                uint childInstanceIndex = instancesInZone[clusterChildCore.Pointer];
                                chainedPuzzlesInfo.Append($"puzzle index: {j}\n");
                                chainedPuzzlesInfo.Append("type: CP_Bioscan_Core\n");
                                chainedPuzzlesInfo.Append($"PuzzleOverrideIndex: {childInstanceIndex}\n");
                            }
                            chainedPuzzlesInfo.Append("=== Clustered puzzles END ===\n");
                        }
                    }

                    else
                    {
                        ScanPosOverrideLogger.Error("Unregistered iChainedPuzzleCore found...");
                    }
                }
                chainedPuzzlesInfo.Append('\n');
            }

            ScanPosOverrideLogger.Debug(chainedPuzzlesInfo.ToString());
        }

        public void Clear() { }

        private PuzzleInstanceManager() 
        {
            LevelAPI.OnEnterLevel += OutputLevelPuzzleInfo;
            LevelAPI.OnBuildStart += Clear;
            LevelAPI.OnLevelCleanup += Clear;
        }

        static PuzzleInstanceManager() {}

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
                ScanPosOverrideLogger.Error("Failed to find CP_BioScan_Core owner (instance of ChainedPuzzleInstance).");
                return null;
            }
        }

        public override (eDimensionIndex, LG_LayerType, eLocalZoneIndex) GetGlobalZoneIndex(PuzzleWrapper instance) 
            => (instance.courseNode.m_dimension.DimensionIndex, instance.courseNode.LayerType, instance.courseNode.m_zone.LocalIndex);

        public uint GetZoneInstanceIndex(CP_Bioscan_Core core)
        {
            if(core.CourseNode == null) // yet not setup
            {
                return INVALID_INSTANCE_INDEX;
            }

            var globalZoneIndex = (core.m_courseNode.m_dimension.DimensionIndex, core.m_courseNode.LayerType, core.m_courseNode.m_zone.LocalIndex);

            if (!instances2Index.ContainsKey(globalZoneIndex)) return INVALID_INSTANCE_INDEX;

            var zoneInstanceIndices = instances2Index[globalZoneIndex];
            return zoneInstanceIndices.ContainsKey(core.Pointer) ? zoneInstanceIndices[core.Pointer] : INVALID_INSTANCE_INDEX;
        }

        public uint GetZoneInstanceIndex(CP_Cluster_Core core)
        {
            if (core.m_owner == null) // yet not setup
            {
                return INVALID_INSTANCE_INDEX;
            }

            var owner = core.m_owner.Cast<ChainedPuzzleInstance>();
            var node = owner.m_sourceArea.m_courseNode;
            var globalZoneIndex = (node.m_dimension.DimensionIndex, node.LayerType, node.m_zone.LocalIndex);

            if (!instances2Index.ContainsKey(globalZoneIndex)) return INVALID_INSTANCE_INDEX;

            var zoneInstanceIndices = instances2Index[globalZoneIndex];
            return zoneInstanceIndices.ContainsKey(core.Pointer) ? zoneInstanceIndices[core.Pointer] : INVALID_INSTANCE_INDEX;
        }
    }
}
