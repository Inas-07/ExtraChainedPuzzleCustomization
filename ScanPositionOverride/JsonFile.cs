namespace ScanPositionOverride;

internal sealed class JsonFile
{
    public uint MainLevelLayout { get; set; }
    public List<PuzzleFile> Puzzles { get; set; } = new();
}
