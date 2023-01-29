using HarmonyLib;

namespace ScanPositionOverride;

[PluginAttributes("ScanPositionOverride", "ScanPositionOverride", "1.0.0")]
internal sealed class Plugin : BasePlugin
{
    public override void Load()
    {
        OnBioscan += MoveBio;
        OnBioscan += LogBio;
        OnClusterscan += MoveCluster;
        OnClusterscan += LogCluster;

        EventAPI.OnExpeditionStarted += OnExpeditionStarted;
        string targetPath = Path.Combine(BepInExPaths.BepInExRootPath, "GameData", "ScanPositionOverrides");
        Directory.CreateDirectory(targetPath)
            .EnumerateFiles()
            .Where(file => file.Extension.Contains(".json"))
            .Where(file => !file.Name.Contains("Template"))
            .Do(Deserialize);

        var templatePath = Path.Combine(targetPath, "Template.json");
        if (File.Exists(templatePath)) return;
        var templateFile = File.OpenWrite(templatePath);
        var jsonFile = new JsonFile();
        var puzzle = new PuzzleFile();
        puzzle.TPositions.Add(new());
        jsonFile.Puzzles.Add(puzzle);
        JsonSerializer.Serialize(templateFile, jsonFile, JsonOptions);
    }

    private void Deserialize(FileInfo info)
    {
        var setting = JsonSerializer.Deserialize<JsonFile?>(info.OpenRead(), JsonOptions);
        if (setting == null) return;

        List<PuzzleOverride>? puzzleOverrides;

        if (!PuzzleOverrides.TryGetValue(setting.MainLevelLayout, out puzzleOverrides))
        {
            puzzleOverrides = new();
            PuzzleOverrides.Add(setting.MainLevelLayout, puzzleOverrides);
        }

        for (int i = 0; i < setting.Puzzles.Count; i++)
        {
            var pOverride = new PuzzleOverride()
            {
                PuzzleIndex = setting.Puzzles[i].Index,
                position = setting.Puzzles[i].Position.ToVector3(),
                rotation = setting.Puzzles[i].Rotation.ToQuaternion(),
                positions = new()
            };
            foreach (var pos in setting.Puzzles[i].TPositions)
            {
                pOverride.positions.Add(pos.ToVector3());
            }
            puzzleOverrides.Add(pOverride);
        }
    }

    private void OnExpeditionStarted()
    {
        byte count = 0;
        Bioscans bioscans;
        Clusterscans clusterscans;

        for (int a = 0; a < ChainedPuzzleManager.Current.m_instances.Count; a++)
        {
            bioscans = ChainedPuzzleManager.Current.m_instances[a].m_parent.GetComponentsInChildren<Bioscan>();
            if (bioscans != null)
            {
                for (int b = 0; b < bioscans.Count; b++)
                {
                    OnBioscan?.Invoke(count, bioscans[b]);
                    count++;
                }
            }
            clusterscans = ChainedPuzzleManager.Current.m_instances[a].m_parent.GetComponentsInChildren<Clusterscan>();
            if (clusterscans != null)
            {
                for (int c = 0; c < clusterscans.Count; c++)
                {
                    OnClusterscan?.Invoke(count, clusterscans[c]);
                    count++;
                }
            }
        }
    }

    private PuzzleOverride? GetModifaction(byte count)
    {
        if (PuzzleOverrides.TryGetValue(ActiveExpedition, out var puzzleOverrides))
        {
            for(int i = 0; i < puzzleOverrides.Count; i++)
            {
                if (puzzleOverrides[i].PuzzleIndex == count)
                    return puzzleOverrides[i];
            }
        }
        return null;
    }

    private void MoveBio(byte count, Bioscan scan)
    {
        var modification = GetModifaction(count);
        if (modification == null) return;
        scan.gameObject.transform.position = modification.position;
        scan.gameObject.transform.rotation = modification.rotation;
        if (scan.m_isMovable && modification.positions.Count > 0)
        {
            scan.m_movingComp.ScanPositions = modification.positions;
        }
    }

    private void LogBio(byte count, Bioscan scan)
    {
        LogScan(count, scan.gameObject);
        if (scan.m_isMovable)
        {
            Log.LogDebug("move positions");
            Vector3 pos;
            for(int i = 0; i < scan.m_movingComp.ScanPositions.Count; i++)
            {
                pos = scan.m_movingComp.ScanPositions[i];
                Log.LogDebug($"{pos.x},{pos.y},{pos.z}");
            }
        }
    }

    private void MoveCluster(byte count, Clusterscan scan)
    {
        var modification = GetModifaction(count);
        if (modification == null) return;
        scan.gameObject.transform.position = modification.position;
        scan.gameObject.transform.rotation = modification.rotation;
    }
    
    private void LogCluster(byte count, Clusterscan scan)
    {
        LogScan(count, scan.gameObject);
    }

    private void LogScan(byte count, GameObject scan)
    {
        var pos = scan.transform.position;
        var rot = scan.transform.rotation.ToEulerAngles();
        Log.LogDebug($"layout:{ActiveExpedition} index:{count} pos:{pos.x},{pos.y},{pos.z} rot:{rot.x},{rot.y},{rot.z} name:{scan.name}");
    }

    private GameEventLog GameEventLog
    {
        get
        {
            if (gameEventLog == null)
            {
                gameEventLog = GuiManager.PlayerLayer.m_gameEventLog;
            }
            return gameEventLog;
        }
    }

    private uint ActiveExpedition
    {
        get
        {
            return RundownManager.ActiveExpedition.LevelLayoutData;
        }
    }

    private GameEventLog? gameEventLog;
    private Action<byte, Bioscan>? OnBioscan;
    private Action<byte, Clusterscan>? OnClusterscan;
    private JsonSerializerOptions JsonOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = false, WriteIndented = true };
    private Dictionary<uint, List<PuzzleOverride>> PuzzleOverrides = new();
}