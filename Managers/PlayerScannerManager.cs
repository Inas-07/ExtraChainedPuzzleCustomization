using ChainedPuzzles;
using System.Collections.Generic;
using GTFO.API;
using System.Linq;
using System.Threading;

namespace ScanPosOverride.Managers
{
    public class PlayerScannerManager
    {
        public static readonly PlayerScannerManager Current;

        // concurrent cluster scan registration.
        private Dictionary<CP_Cluster_Core, List<CP_PlayerScanner>> ConcurrentClusterCores = new();
        private Dictionary<System.IntPtr, CP_Cluster_Core> ConcurrentScanClusterParents = new();

        // concurrent cluster scan state
        // we use HashSet.Count to evaluate if all child scans satisfy progressing requirement and should thus progress
        private Dictionary<CP_Cluster_Core, HashSet<System.IntPtr>> ConcurrentClusterChildScanState = new();
        private Mutex ConcurrentClusterStateMutex = null;

        // original scan speed
        private Dictionary<CP_Cluster_Core, float[]> OriginalClusterScanSpeeds = new();
        
        // (cached) scanners for relevant CP_Bioscan_Core
        private Dictionary<System.IntPtr, CP_PlayerScanner> Scanners = new();

        // invoked after core.Setup()
        public bool RegisterConcurrentCluster(CP_Cluster_Core core)
        {
            if (ConcurrentClusterCores.ContainsKey(core)) return false;
            List<CP_PlayerScanner> childScanners = Enumerable.Repeat<CP_PlayerScanner>(null, core.m_amountOfPuzzles).ToList();

            HashSet<CP_Bioscan_Core> childCores = new();

            float[] originalScanSpeed = new float[4];
            for (int childIndex = 0; childIndex < core.m_childCores.Count; childIndex++)
            {
                if (childScanners[childIndex] != null)
                {
                    Logger.Error("SetupConcurrentClusterScanners: Duplicate child scanner for child scan. ??");
                    continue;
                }

                var IChildCore = core.m_childCores[childIndex];

                CP_Bioscan_Core bioscanCore = IChildCore.TryCast<CP_Bioscan_Core>();
                if (bioscanCore == null)
                {
                    Logger.Error("SetupConcurrentClusterScanners: Failed to cast child to CP_Bioscan_Core");
                    continue;
                }

                CP_PlayerScanner scanner = bioscanCore.PlayerScanner.TryCast<CP_PlayerScanner>();
                if (scanner == null)
                {
                    Logger.Error("SetupConcurrentClusterScanners: Failed to cast CP_Bioscan_Core.PlayerScanner to CP_PlayerScanner");
                    continue;
                }

                childScanners[childIndex] = scanner;
                Scanners.Add(IChildCore.Pointer, scanner);

                if (!OriginalClusterScanSpeeds.ContainsKey(core))
                {
                    var speeds = scanner.m_scanSpeeds;
                    for(int i = 0; i < 4; i++)
                    {
                        originalScanSpeed[i] = speeds[i];
                    }

                    OriginalClusterScanSpeeds.Add(core, originalScanSpeed);
                }

                ConcurrentScanClusterParents.Add(IChildCore.Pointer, core);
            }

            ConcurrentClusterCores.Add(core, childScanners);
            ConcurrentClusterChildScanState.Add(core, new());
            return true;
        }

        //public CP_Cluster_Core GetParentClusterCore(CP_PlayerScanner scanner) => ParentCluster.ContainsKey(scanner) ? ParentCluster[scanner] : null;

        internal bool IsConcurrentCluster(CP_Cluster_Core core) => ConcurrentClusterCores.ContainsKey(core);

        internal bool IsConcurrentCluster(CP_Bioscan_Core core) => ConcurrentScanClusterParents.ContainsKey(core.Pointer);

        internal void ZeroConcurrentClusterScanSpeed(CP_Cluster_Core clusterCore)
        {
            if (!ConcurrentClusterCores.ContainsKey(clusterCore)) return;
            foreach(var childScanner in ConcurrentClusterCores[clusterCore])
            {
                for (int i = 0; i < 4; i++)
                {
                    childScanner.m_scanSpeeds[i] = 0.0f;
                }
            }
        }

        internal void RestoreConcurrentClusterScanSpeed(CP_Cluster_Core clusterCore)
        {
            if (!ConcurrentClusterCores.ContainsKey(clusterCore) || !OriginalClusterScanSpeeds.ContainsKey(clusterCore)) return;
            float[] originalScanSpeed = OriginalClusterScanSpeeds[clusterCore];

            foreach (var childScanner in ConcurrentClusterCores[clusterCore])
            {
                for (int i = 0; i < 4; i++)
                {
                    childScanner.m_scanSpeeds[i] = originalScanSpeed[i];
                }
            }
        }

        internal float[] GetOriginalScanSpeed(CP_Bioscan_Core core)
        {
            if (!ConcurrentScanClusterParents.ContainsKey(core.Pointer)) return new float[4] { 0.0f, 0.0f, 0.0f, 0.0f };
            CP_Cluster_Core parent = ConcurrentScanClusterParents[core.Pointer];

            return OriginalClusterScanSpeeds.ContainsKey(parent) ? OriginalClusterScanSpeeds[parent] : new float[4] { 0.0f, 0.0f, 0.0f, 0.0f };
        }

        // get scanner for concurrent cluster & T scan
        public CP_PlayerScanner GetCacheScanner(CP_Bioscan_Core core)
        {
            if(Scanners.ContainsKey(core.Pointer)) return Scanners[core.Pointer];

            CP_PlayerScanner scanner = core.PlayerScanner.TryCast<CP_PlayerScanner>();
            if (scanner == null) return null;

            Scanners.Add(core.Pointer, scanner);

            return scanner;
        }

        internal bool ConcurrentClusterShouldProgress(CP_Bioscan_Core core, bool IsThisScanShouldProgress)
        {
            if(ConcurrentClusterStateMutex == null)
            {
                Logger.Error("ConcurrentCluster: scan mutex uninitialized.");
                return false;
            }

            if (ConcurrentClusterStateMutex.WaitOne(2000))
            {
                if(!ConcurrentScanClusterParents.ContainsKey(core.Pointer))
                {
                    Logger.Error("ConcurrentClusterShouldProgress: failed to find cluster parent!");
                    ConcurrentClusterStateMutex.ReleaseMutex();
                    return false;
                }

                CP_Cluster_Core clusterParent = ConcurrentScanClusterParents[core.Pointer];
                if (!ConcurrentClusterChildScanState.ContainsKey(clusterParent))
                {
                    Logger.Error("ConcurrentClusterShouldProgress: ConcurrentClusterChildScanState initialization error!");
                    ConcurrentClusterStateMutex.ReleaseMutex();
                    return false;
                }

                var childScanState = ConcurrentClusterChildScanState[clusterParent];
                bool ScanShouldProgress;
                if (IsThisScanShouldProgress)
                {
                    childScanState.Add(core.Pointer);
                    ScanShouldProgress = childScanState.Count == clusterParent.m_amountOfPuzzles;
                }
                else
                {
                    childScanState.Remove(core.Pointer);
                    ScanShouldProgress = false;
                }

                Logger.Debug($"Concurrent cluster: {clusterParent.m_amountOfPuzzles} children, {childScanState.Count} children should progress.");

                // Release the Mutex.
                ConcurrentClusterStateMutex.ReleaseMutex();
                return ScanShouldProgress;
            }
            else
            {
                Logger.Debug("ConcurrentCluster: Failed to acquire scan mutex.");
                return false;
            }
        }

        public void Init()
        {
            ConcurrentClusterStateMutex = new();
        }

        public void Clear()
        {
            ConcurrentClusterCores.Clear();
            OriginalClusterScanSpeeds.Clear();
            ConcurrentScanClusterParents.Clear();
            Scanners.Clear();
            ConcurrentClusterStateMutex.Dispose();
            ConcurrentClusterStateMutex = null;
        }

        static PlayerScannerManager() 
        { 
            Current = new PlayerScannerManager();
            LevelAPI.OnEnterLevel += Current.Init;
            LevelAPI.OnLevelCleanup += Current.Clear;
        }

        private PlayerScannerManager() { }
    }
}
