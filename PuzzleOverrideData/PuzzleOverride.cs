using System.Collections.Generic;
using GameData;

namespace ScanPosOverride.PuzzleOverrideData
{
    public class BioscanProgressEvent 
    {
        public float Progress { get; set; } = -1.0f;

        public List<WardenObjectiveEventData> Events { get; set; } = new();
    }

    public class ClusterProgressEvent
    {
        public int Count { get; set; } = -1;

        public List<WardenObjectiveEventData> Events { get; set; } = new();
    }

    public class PuzzleOverride
    {
        public uint Index { get; set; }

        public Vec3 Position { get; set; } = new Vec3();

        public Vec3 Rotation { get; set; } = new Vec3();

        public bool HideSpline { get; set; } = false;

        public bool ConcurrentCluster { get; set; } = false;

        public float TMoveSpeedMulti { get; set; } = -1.0f;

        public List<BioscanProgressEvent> EventsOnBioscanProgress { get; set; } = new() { new() };

        public List<ClusterProgressEvent> EventsOnClusterProgress { get; set; } = new() { new() };

        public List<Vec3> TPositions { get; set; } = new List<Vec3>();

        public List<int> RequiredItemsIndices { get; set; } = new() { 0 };

        public List<WardenObjectiveEventData> EventsOnPuzzleSolved { get; set; } = new();
    }
}
