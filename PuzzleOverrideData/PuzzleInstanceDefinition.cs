using ExtraObjectiveSetup.BaseClasses;
using ChainedPuzzles;
using System.Text.Json.Serialization;
using GameData;
using System.Collections.Generic;

namespace ScanPosOverride.PuzzleOverrideData
{
    public class PuzzleInstanceDefinition: BaseInstanceDefinition
    {
        public uint Index { get; set; }  // TODO: deprecate this in the future

        public Vec3 Position { get; set; } = new Vec3();

        public Vec3 Rotation { get; set; } = new Vec3();

        public bool HideSpline { get; set; } = false;

        public bool ConcurrentCluster { get; set; } = false;

        public float TMoveSpeedMulti { get; set; } = -1.0f;

        public List<Vec3> TPositions { get; set; } = new List<Vec3>();

        public List<int> RequiredItemsIndices { get; set; } = new() { 0 };

        public List<WardenObjectiveEventData> EventsOnPuzzleActivate { get; set; } = new();

        public List<WardenObjectiveEventData> EventsOnPuzzleSolved { get; set; } = new();

        [JsonIgnore]
        public CP_Bioscan_Core bioscan_Core { get; internal set; } = null;

        [JsonIgnore]
        public CP_Cluster_Core cluster_Core { get; internal set; } = null;
    }
}
