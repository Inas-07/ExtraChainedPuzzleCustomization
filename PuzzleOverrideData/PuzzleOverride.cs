using System.Collections.Generic;
using GameData;
namespace ScanPosOverride.PuzzleOverrideData
{
    internal sealed class PuzzleOverride
    {
        public byte Index { get; set; }

        public Vec3 Position { get; set; } = new Vec3();

        public Vec3 Rotation { get; set; } = new Vec3();

        // system list
        public List<Vec3> TPositions { get; set; } = new List<Vec3>();

        public List<WardenObjectiveEventData> EventsOnPuzzleSolved { get; set; } = new();
    }
}
