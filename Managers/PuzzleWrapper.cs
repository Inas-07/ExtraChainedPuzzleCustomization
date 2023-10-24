using AIGraph;
using ChainedPuzzles;
using System;

namespace ScanPosOverride.Managers
{
    public class PuzzleWrapper : Il2CppSystem.Object
    {
        public CP_Bioscan_Core bioscan_Core { get; private set; } = null;

        public CP_Cluster_Core cluster_Core { get; private set; } = null;

        public new IntPtr Pointer => bioscan_Core != null ? bioscan_Core.Pointer : cluster_Core.Pointer;

        public AIG_CourseNode courseNode { get; private set; }

        public PuzzleWrapper(CP_Bioscan_Core bioscan_Core)
        {
            if(bioscan_Core == null || bioscan_Core.m_courseNode == null) 
                throw new ArgumentNullException("Passed in null bioscan_core, or courseNode of the core is not setup properly");
            
            this.bioscan_Core = bioscan_Core;
            this.courseNode = bioscan_Core.m_courseNode;
        }

        public PuzzleWrapper(CP_Bioscan_Core bioscan_Core, AIG_CourseNode srcNode)
        {
            if (bioscan_Core == null || srcNode == null)
                throw new ArgumentNullException("Passed in null bioscan_core or courseNode");
            
            this.bioscan_Core = bioscan_Core;
            if (bioscan_Core.m_courseNode != null)
            {
                ScanPosOverrideLogger.Warning("Instantiating PuzzleWrapper: passed in bioscan_core has been setup properly. Prefer instantiating without passing in srcNode");
                this.courseNode = bioscan_Core.m_courseNode;
            }
            else
            {
                this.courseNode = srcNode;
            }
        }

        public PuzzleWrapper(CP_Cluster_Core cluster_Core)
        {
            if (cluster_Core == null || cluster_Core.m_owner == null)
                throw new ArgumentNullException("Passed in null cluster_core, or owner of the core is not setup properly");

            this.cluster_Core = cluster_Core;
            this.courseNode = cluster_Core.m_owner.Cast<ChainedPuzzleInstance>().m_sourceArea.m_courseNode;
        }

        public PuzzleWrapper(CP_Cluster_Core cluster_Core, AIG_CourseNode srcNode)
        {
            if (cluster_Core == null || srcNode == null) 
                throw new ArgumentNullException("Passed in null cluster_core or srcNode");
            
            this.cluster_Core = cluster_Core;
            if (cluster_Core.m_owner != null)
            {
                ScanPosOverrideLogger.Warning("Instantiating PuzzleWrapper: passed in owner_core has been setup properly. Prefer instantiating without passing in srcNode");
                this.courseNode = cluster_Core.m_owner.Cast<ChainedPuzzleInstance>().m_sourceArea.m_courseNode;
            }
            else
            {
                this.courseNode = srcNode;
            }
        }
    }
}
