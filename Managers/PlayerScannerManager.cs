using ChainedPuzzles;
using System.Collections.Generic;
using GTFO.API;
using System.Linq;
using System.Threading;
using System;

namespace ScanPosOverride.Managers
{
    public class PlayerScannerManager
    {
        private static readonly float[] ZERO_SCAN_SPEED = new float[4] { 0.0f, 0.0f, 0.0f, 0.0f };

        public static PlayerScannerManager Current { get; }

        // concurrent cluster scan registration.
        // key: CP_Cluster_Core.Pointer, value: PlayerScanners of its childCores
        private Dictionary<IntPtr, List<CP_PlayerScanner>> CCCores { get; } = new();

        // key: CP_Cluster_Core.Pointer, value: IntPtr HashSet of its childCores
        //private Dictionary<IntPtr, List<CP_Bioscan_Core>> CCChildren { get; } = new();
        private Dictionary<IntPtr, HashSet<IntPtr>> CCChildren { get; } = new();


        // concurrent cluster scan state
        // we use HashSet.Count to evaluate if all child scans satisfy progressing requirement and should thus progress
        // key: CP_Cluster_Core.Pointer, value: child states
        private Dictionary<IntPtr, HashSet<IntPtr>> CCChildrenState { get; } = new();

        private Mutex CCStateMutex = null;

        // Key: CP_Cluster_Core.Pointer, value: children scan speed
        private Dictionary<IntPtr, float[]> OriginalClusterScanSpeeds { get; } = new();

        // (cached) scanners for relevant CP_Bioscan_Core
        // Key: CP_Bioscan_Core.Pointer, value: children scan speed
        private Dictionary<IntPtr, CP_PlayerScanner> ChildScanners = new();

        // Key: CP_Bioscan_Core.Pointer, value: children scan speed
        private Dictionary<IntPtr, float[]> OriginalScanSpeed { get; } = new();

        // invoked after core.Setup()
        internal bool RegisterConcurrentCluster(CP_Cluster_Core core)
        {
            if (CCCores.ContainsKey(core.Pointer)) return false;
            List<CP_PlayerScanner> scanners = Enumerable.Repeat<CP_PlayerScanner>(null, core.m_amountOfPuzzles).ToList();
            //List<CP_Bioscan_Core> childCores = Enumerable.Repeat<CP_Bioscan_Core>(null, core.m_amountOfPuzzles).ToList();

            var childCores = new HashSet<IntPtr>();

            for (int childIndex = 0; childIndex < core.m_childCores.Count; childIndex++)
            {
                if (scanners[childIndex] != null)
                {
                    SPOLogger.Error("SetupConcurrentClusterScanners: Duplicate child scanner for child scan. ??");
                    continue;
                }

                var IChildCore = core.m_childCores[childIndex];

                CP_Bioscan_Core child = IChildCore.TryCast<CP_Bioscan_Core>();
                if (child == null)
                {
                    SPOLogger.Error("SetupConcurrentClusterScanners: Failed to cast child to CP_Bioscan_Core");
                    continue;
                }

                CP_PlayerScanner scanner = child.PlayerScanner.TryCast<CP_PlayerScanner>();
                if (scanner == null)
                {
                    SPOLogger.Error("SetupConcurrentClusterScanners: Failed to cast CP_Bioscan_Core.PlayerScanner to CP_PlayerScanner");
                    continue;
                }

                scanners[childIndex] = scanner;
                ChildScanners.Add(IChildCore.Pointer, scanner);
                childCores.Add(child.Pointer);

                if (!OriginalClusterScanSpeeds.ContainsKey(core.Pointer))
                {
                    var speeds = scanner.m_scanSpeeds;
                    float[] originalScanSpeed = new float[speeds.Length];
                    Array.Copy(speeds, originalScanSpeed, speeds.Length);
                    OriginalClusterScanSpeeds.Add(core.Pointer, originalScanSpeed);
                }
            }

            CCCores.Add(core.Pointer, scanners);
            CCChildren.Add(core.Pointer, childCores);
            CCChildrenState.Add(core.Pointer, new());
            return true;
        }

        internal bool IsConcurrentCluster(CP_Cluster_Core core) => CCCores.ContainsKey(core.Pointer);

        internal bool IsConcurrentCluster(CP_Bioscan_Core core) => CCCores.ContainsKey(core.Owner.Pointer);

        internal void ZeroCCScanSpeed(CP_Cluster_Core parent)
        {
            if (!CCCores.ContainsKey(parent.Pointer)) return;
            foreach(var childScanner in CCCores[parent.Pointer])
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

        internal void RestoreCCScanSpeed(CP_Cluster_Core parent)
        {
            if (!CCCores.ContainsKey(parent.Pointer) || !OriginalClusterScanSpeeds.ContainsKey(parent.Pointer)) return;
            float[] originalScanSpeed = OriginalClusterScanSpeeds[parent.Pointer];

            foreach (var childScanner in CCCores[parent.Pointer])
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
                return CCCores.ContainsKey(core.Owner.Pointer) ? OriginalClusterScanSpeeds[core.Owner.Pointer] : ZERO_SCAN_SPEED;
            }

            else
            {
                if(OriginalScanSpeed.ContainsKey(core.Pointer)) return OriginalScanSpeed[core.Pointer];

                CP_PlayerScanner scanner = GetCacheScanner(core);
                if(scanner == null)
                {
                    SPOLogger.Error($"GetCacheOriginalScanSpeed: cannot get scanner for this CP_Bioscan_Core");
                    return ZERO_SCAN_SPEED;
                }

                float[] scanSpeeds = new float[4];
                Array.Copy(scanner.m_scanSpeeds, scanSpeeds, scanSpeeds.Length);
                OriginalScanSpeed.Add(core.Pointer, scanSpeeds);
                return scanSpeeds;
            }
        }

        // get scanner for concurrent cluster & T scan
        public CP_PlayerScanner GetCacheScanner(CP_Bioscan_Core core)
        {
            if(ChildScanners.ContainsKey(core.Pointer)) return ChildScanners[core.Pointer];

            CP_PlayerScanner scanner = core.PlayerScanner.TryCast<CP_PlayerScanner>();
            if (scanner == null) return null;

            ChildScanners.Add(core.Pointer, scanner);

            return scanner;
        }

        internal bool CCShouldProgress(CP_Bioscan_Core child, bool IsThisScanShouldProgress)
        {
            if(CCStateMutex == null)
            {
                CCStateMutex = new();
            }

            if (CCStateMutex.WaitOne(2000))
            {
                if(!CCCores.ContainsKey(child.Owner.Pointer))
                {
                    SPOLogger.Error("ConcurrentClusterShouldProgress: failed to find cluster parent!");
                    CCStateMutex.ReleaseMutex();
                    return false;
                }

                CP_Cluster_Core parent = child.Owner.Cast<CP_Cluster_Core>();
                if (!CCChildrenState.ContainsKey(parent.Pointer))
                {
                    SPOLogger.Error("ConcurrentClusterShouldProgress: ConcurrentClusterChildScanState initialization error!");
                    CCStateMutex.ReleaseMutex();
                    return false;
                }

                var childScanState = CCChildrenState[parent.Pointer];
                bool ScanShouldProgress;
                if (IsThisScanShouldProgress)
                {
                    childScanState.Add(child.Pointer);
                    ScanShouldProgress = childScanState.Count == parent.m_amountOfPuzzles;
                }
                else
                {
                    childScanState.Remove(child.Pointer);
                    ScanShouldProgress = false;
                }

                //Logger.Debug($"Concurrent cluster: {clusterParent.m_amountOfPuzzles} children, {childScanState.Count} children should progress.");

                // Release the Mutex.
                CCStateMutex.ReleaseMutex();
                return ScanShouldProgress;
            }
            else
            {
                SPOLogger.Debug("ConcurrentCluster: Failed to acquire scan mutex.");
                return false;
            }
        }

        internal void CompleteConcurrentCluster(CP_Cluster_Core parent, CP_Bioscan_Core child)
        {
            if (!CCChildren.ContainsKey(parent.Pointer)) return;

            var childCores = CCChildren[parent.Pointer];

            //foreach (var child in childCores)
            //{
            //    child.m_sync.SetStateData(eBioscanStatus.Finished);
            //}

            // NOTE: changed behavior, to handle case like booster increasing scan speed of a child puzzle
            childCores.Remove(child.Pointer);
            if(childCores.Count < 1)
            {
                CCChildren.Remove(parent.Pointer);
            }
        }

        public List<CP_PlayerScanner> GetCCChildrenScanner(CP_Cluster_Core parent) => CCCores.ContainsKey(parent.Pointer) ? CCCores[parent.Pointer] : null;

        public void Clear()
        {
            CCCores.Clear();
            CCChildren.Clear();
            CCChildrenState.Clear();
            OriginalClusterScanSpeeds.Clear();
            ChildScanners.Clear();
            OriginalScanSpeed.Clear();

            if (CCStateMutex != null)
            {
                CCStateMutex.Dispose();
            }

            CCStateMutex = null;
        }

        static PlayerScannerManager() 
        { 
            Current = new PlayerScannerManager();
            LevelAPI.OnLevelCleanup += Current.Clear;
        }

        private PlayerScannerManager() { }
    }
}
