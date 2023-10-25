using System.Collections.Generic;
using GameData;
namespace ScanPosOverride.PuzzleOverrideData
{
    internal sealed class PuzzleOverride
    {
        public uint Index { get; set; }

        public Vec3 Position { get; set; } = new Vec3();

        public Vec3 Rotation { get; set; } = new Vec3();

        public bool HideSpline { get; set; } = false;

        public bool ConcurrentCluster { get; set; } = false;

        public float TMoveSpeedMulti { get; set; } = -1.0f;

        public List<Vec3> TPositions { get; set; } = new List<Vec3>();

        public List<int> RequiredItemsIndices { get; set; } = new() { 0 };

        public List<WardenObjectiveEventData> EventsOnPuzzleActivate { get; set; } = new();

        public List<WardenObjectiveEventData> EventsOnPuzzleSolved { get; set; } = new();
    }
}
