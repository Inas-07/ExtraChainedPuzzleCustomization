namespace ScanPositionOverride;

internal sealed class PuzzleOverride
{
    public byte PuzzleIndex { get; set; }
    public Vector3 position { get; set; }
    public Quaternion rotation { get; set; }
    public Il2CppSystem.Collections.Generic.List<Vector3> positions { get; set; }
}
