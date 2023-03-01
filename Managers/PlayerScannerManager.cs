using ChainedPuzzles;
using System.Collections.Generic;
using GTFO.API;
using System.Linq;
using System.Threading;
using System.Runtime.CompilerServices;

namespace ScanPosOverride.Managers
{
    public class PlayerScannerManager
    {
        public static readonly PlayerScannerManager Current;

        // concurrent cluster scan registration.
        private Dictionary<CP_Cluster_Core, List<CP_PlayerScanner>> ConcurrentClusterCores = new();
        private Dictionary<CP_Cluster_Core, List<CP_Bioscan_Core>> ConcurrentClusterChildCores = new();
        private Dictionary<System.IntPtr, CP_Cluster_Core> ConcurrentScanClusterParents = new();

        // concurrent cluster scan state
        // we use HashSet.Count to evaluate if all child scans satisfy progressing requirement and should thus progress
        private Dictionary<CP_Cluster_Core, HashSet<System.IntPtr>> ConcurrentClusterChildScanState = new();
        private Mutex ConcurrentClusterStateMutex = null;

        // original scan speed
        private Dictionary<CP_Cluster_Core, float[]> OriginalClusterScanSpeeds = new();
        
        // (cached) scanners for relevant CP_Bioscan_Core
        private Dictionary<System.IntPtr, CP_PlayerScanner> Scanners = new();

        private Dictionary<System.IntPtr, float[]> OriginalScanSpeed = new();

        private static readonly float[] ZERO_SCAN_SPEED = new float[4] { 0.0f, 0.0f, 0.0f, 0.0f } ;

        // invoked after core.Setup()
        internal bool RegisterConcurrentCluster(CP_Cluster_Core core)
        {
            if (ConcurrentClusterCores.ContainsKey(core)) return false;
            List<CP_PlayerScanner> childScanners = Enumerable.Repeat<CP_PlayerScanner>(null, core.m_amountOfPuzzles).ToList();
            List<CP_Bioscan_Core> childCores = Enumerable.Repeat<CP_Bioscan_Core>(null, core.m_amountOfPuzzles).ToList();

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
                childCores[childIndex] = bioscanCore;

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
            ConcurrentClusterChildCores.Add(core, childCores);
            ConcurrentClusterChildScanState.Add(core, new());
            return true;
        }

        internal bool IsConcurrentCluster(CP_Cluster_Core core) => ConcurrentClusterCores.ContainsKey(core);

        internal bool IsConcurrentCluster(CP_Bioscan_Core core) => ConcurrentScanClusterParents.ContainsKey(core.Pointer);

        internal void ZeroConcurrentClusterScanSpeed(CP_Cluster_Core clusterCore)
        {
            if (!ConcurrentClusterCores.ContainsKey(clusterCore)) return;
            foreach(var childScanner in ConcurrentClusterCores[clusterCore])
            {
                bool IsAlreadyZeroed = true;
                for (int i = 0; i < 4; i++)
                {
                    IsAlreadyZeroed = IsAlreadyZeroed && childScanner.m_scanSpeeds[i] == 0.0f;
                    childScanner.m_scanSpeeds[i] = 0.0f;
                }

                if (IsAlreadyZeroed) break;
            }
        }

        internal void RestoreConcurrentClusterScanSpeed(CP_Cluster_Core clusterCore)
        {
            if (!ConcurrentClusterCores.ContainsKey(clusterCore) || !OriginalClusterScanSpeeds.ContainsKey(clusterCore)) return;
            float[] originalScanSpeed = OriginalClusterScanSpeeds[clusterCore];

            foreach (var childScanner in ConcurrentClusterCores[clusterCore])
            {
                bool IsAlreadyRestored = false;
                for (int i = 0; i < 4; i++)
                {
                    IsAlreadyRestored = IsAlreadyRestored || childScanner.m_scanSpeeds[i] != 0.0f;
                    childScanner.m_scanSpeeds[i] = originalScanSpeed[i];
                }

                if (IsAlreadyRestored) break;
            }
        }

        // Note: the cached scan speed here cannot replaced by GetCacheScanner.m_scanSpeed!
        //       We modify the latter to do some bug fix && implement ConcurrentCluster!
        internal float[] GetCacheOriginalScanSpeed(CP_Bioscan_Core core)
        {
            if(IsConcurrentCluster(core))
            {
                if (!ConcurrentScanClusterParents.ContainsKey(core.Pointer)) return ZERO_SCAN_SPEED;
                CP_Cluster_Core parent = ConcurrentScanClusterParents[core.Pointer];

                return OriginalClusterScanSpeeds.ContainsKey(parent) ? OriginalClusterScanSpeeds[parent] : ZERO_SCAN_SPEED;
            }

            else
            {
                if(OriginalScanSpeed.ContainsKey(core.Pointer)) return OriginalScanSpeed[core.Pointer];

                CP_PlayerScanner scanner = GetCacheScanner(core);
                if(scanner == null)
                {
                    Logger.Error($"GetCacheOriginalScanSpeed: cannot get scanner for this CP_Bioscan_Core");
                    return ZERO_SCAN_SPEED;
                }

                float[] scanSpeeds = new float[4];
                for(int i = 0; i < 4; i++)
                {
                    scanSpeeds[i] = scanner.m_scanSpeeds[i];
                }

                OriginalScanSpeed.Add(core.Pointer, scanSpeeds);
                return scanSpeeds;
            }
        }

        internal CP_Cluster_Core GetParentClusterCore(CP_Bioscan_Core core) => ConcurrentScanClusterParents.ContainsKey(core.Pointer) ? ConcurrentScanClusterParents[core.Pointer] : null;

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

        internal void CompleteConcurrentCluster(CP_Cluster_Core core)
        {
            if (!ConcurrentClusterChildCores.ContainsKey(core)) return;

            var childCores = ConcurrentClusterChildCores[core];

            ConcurrentClusterChildCores.Remove(core);
            foreach (var childCore in childCores)
            {
                childCore.m_sync.SetStateData(eBioscanStatus.Finished);
            }
        }

        public void Init()
        {
            ConcurrentClusterStateMutex = new();
        }

        public void Clear()
        {
            ConcurrentClusterCores.Clear();
            ConcurrentClusterChildCores.Clear();
            ConcurrentScanClusterParents.Clear();
            ConcurrentClusterChildScanState.Clear();
            OriginalClusterScanSpeeds.Clear();
            Scanners.Clear();
            OriginalScanSpeed.Clear();

            if (ConcurrentClusterStateMutex != null)
            {
                ConcurrentClusterStateMutex.Dispose();
            }

            ConcurrentClusterStateMutex = null;
        }

        static PlayerScannerManager() 
        { 
            Current = new PlayerScannerManager();
            LevelAPI.OnBuildDone += Current.Init;
            LevelAPI.OnLevelCleanup += Current.Clear;
        }

        private PlayerScannerManager() { }
    }
}
