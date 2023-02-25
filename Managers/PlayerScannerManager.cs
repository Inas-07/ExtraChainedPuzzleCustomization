using ChainedPuzzles;
using System.Collections.Generic;
using GTFO.API;

namespace ScanPosOverride.Managers
{
    public class PlayerScannerManager
    {
        public static readonly PlayerScannerManager Current;

        private HashSet<CP_Cluster_Core> ConcurrentClusterCores = new();

        public bool RegisterConcurrentCluster(CP_Cluster_Core core) => ConcurrentClusterCores.Add(core);

        public bool IsConcurrentCluster(CP_Cluster_Core core) => ConcurrentClusterCores.Contains(core);



        static PlayerScannerManager() 
        { 
            Current = new PlayerScannerManager();
            LevelAPI.OnLevelCleanup += Current.ConcurrentClusterCores.Clear;
        }

        private PlayerScannerManager() { }
    }
}
