namespace ScanPositionOverride;

internal sealed class PuzzleFile
{
    public byte Index { get; set; }
    public Vec3 Position { get; set; } = new();
    public Vec3 Rotation { get; set; } = new();
    public List<Vec3> TPositions { get; set; } = new();
}
