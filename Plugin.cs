using BepInEx;
using BepInEx.Unity.IL2CPP;
using GTFO.API;
using ScanPosOverride.PuzzleOverrideData;
using System.Collections.Generic;
using System.IO;
using ScanPosOverride.JSON;
using GTFO.API.Utilities;
using HarmonyLib;


namespace ScanPosOverride
{
    [BepInPlugin("ScanPositionOverride", "ScanPositionOverride", "1.1.0")]
    [BepInDependency("dev.gtfomodding.gtfo-api", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(MTFOUtil.PLUGIN_GUID, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(MTFOPartialDataUtil.PLUGIN_GUID, BepInDependency.DependencyFlags.SoftDependency)]

    internal sealed class Plugin : BasePlugin
    {
        // MainLevelLayout, List of puzzles to override
        private static Dictionary<uint, Dictionary<uint, PuzzleOverride>> PuzzleOverrides = new();

        // Map `CP_Bioscan_Core` to the PuzzleOverride Index
        // Note: CP_Cluster_Core is considered the "cluster position"
        //       while CP_Bioscan_Core could both be the clustered scan point and single scan point.

        public static readonly string OVERRIDE_SCAN_POS_PATH = Path.Combine(BepInEx.Paths.BepInExRootPath, "GameData", "ScanPositionOverrides");

        private static LiveEditListener listener = null;
        private static Harmony m_Harmony = null;

        public override void Load()
        {
            Logger.Error(OVERRIDE_SCAN_POS_PATH);
            if (!Directory.Exists(OVERRIDE_SCAN_POS_PATH))
            {
                Logger.Error("Did not find ScanPositionOverrides folder, will not load.");
                return;
            }

            // first reading of all config
            foreach (string config_file in Directory.EnumerateFiles(OVERRIDE_SCAN_POS_PATH, "*.json", SearchOption.AllDirectories))
            {
                PuzzleOverrideJsonFile puzzleOverrideConfig;
                Json.Load(config_file, out puzzleOverrideConfig);

                if (PuzzleOverrides.ContainsKey(puzzleOverrideConfig.MainLevelLayout))
                {
                    Logger.Warning("Duplicate MainLevelLayout {0}, won't load.", puzzleOverrideConfig.MainLevelLayout);
                    continue;
                }

                Dictionary<uint, PuzzleOverride> levelPuzzleToOverride = new();
                foreach(var puzzleToOverride in puzzleOverrideConfig.Puzzles)
                {
                    if(levelPuzzleToOverride.ContainsKey(puzzleToOverride.Index))
                    {
                        Logger.Error("Duplicate Puzzle Override found. MainLevelLayout {0}, Index {1}.", puzzleOverrideConfig.MainLevelLayout, puzzleToOverride.Index);
                        // will not replace.
                        continue;
                    }

                    levelPuzzleToOverride.Add(puzzleToOverride.Index, puzzleToOverride);
                }

                PuzzleOverrides.Add(puzzleOverrideConfig.MainLevelLayout, levelPuzzleToOverride);
            }

            listener = LiveEdit.CreateListener(OVERRIDE_SCAN_POS_PATH, "*.json", includeSubDir: true);
            listener.FileChanged += LiveEdit_FileChanged;

            m_Harmony = new Harmony("ScanPosOverride.Patches");
            m_Harmony.PatchAll();
        }

        private static void LiveEdit_FileChanged(LiveEditEventArgs e)
        {
            Logger.Warning($"LiveEdit File Changed: {e.FullPath}.");

            LiveEdit.TryReadFileContent(e.FullPath, (content) =>
            {
                PuzzleOverrideJsonFile overrideConfig = Json.Deserialize<PuzzleOverrideJsonFile>(content);
                if(!PuzzleOverrides.ContainsKey(overrideConfig.MainLevelLayout))
                {
                    // could also allow replacing.
                    Logger.Warning("MainLevelLayout not found, which is now not supported. Will not replace.");
                    return;
                }

                var levelPuzzleToOverride = PuzzleOverrides[overrideConfig.MainLevelLayout];

                levelPuzzleToOverride.Clear();

                foreach(var puzzleToOverride in overrideConfig.Puzzles)
                {
                    if (levelPuzzleToOverride.ContainsKey(puzzleToOverride.Index))
                    {
                        Logger.Error("Duplicate Puzzle Override found. MainLevelLayout {0}, Index {1}.", overrideConfig.MainLevelLayout, puzzleToOverride.Index);
                        // will not replace.
                        continue;
                    }

                    levelPuzzleToOverride.Add(puzzleToOverride.Index, puzzleToOverride);
                }

                Logger.Warning("Replaced Override Puzzle with MainLevelLayout {0}", overrideConfig.MainLevelLayout);
            });
        }

        /** 
         * @param: mainLevelLayout. 
         * @param: puzzleIndex: the `Index` in the json file. Don't confuse it with the puzzleIndex in CP_Bioscan_Core and CP_Cluster_Core. 
         * @return: puzzle to override, or `null` if there's no override for this puzzle.
        */
        internal static PuzzleOverride GetOverride(uint mainLevelLayout, uint puzzleIndex) 
        {
            if (!PuzzleOverrides.ContainsKey(mainLevelLayout)) return null;

            var levelPuzzleToOverride = PuzzleOverrides[mainLevelLayout];

            if (!levelPuzzleToOverride.ContainsKey(puzzleIndex)) return null;

            return levelPuzzleToOverride[puzzleIndex];
        } 
    }
}
