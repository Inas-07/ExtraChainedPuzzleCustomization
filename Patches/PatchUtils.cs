using AIGraph;
using ChainedPuzzles;
using LevelGeneration;
using UnityEngine;
using System.Collections.Generic;


namespace ScanPosOverride.Patches
{
    public static class Utils
    {
        public static System.Collections.Generic.List<T> ToSystemList<T>(Il2CppSystem.Collections.Generic.List<T> list)
        {
            if(list == null) { return null; }

            System.Collections.Generic.List<T> res = new();

            foreach(T obj in list)
            {
                res.Add(obj);
            }

            return res;
        }

        public static bool TryGetNodes(LG_Area area, bool onlyReachable, out List<AIG_INode> nodes)
        {
            if (area.m_courseNode == null || !area.m_courseNode.IsValid)
            {
                nodes = null;
                return false;
            }
            if (onlyReachable)
            {
                if (area.m_courseNode.m_nodeCluster.m_reachableNodes.Count > 1)
                {
                    nodes = Utils.ToSystemList(area.m_courseNode.m_nodeCluster.m_reachableNodes);
                }
                else
                {
                    nodes = Utils.ToSystemList(area.m_courseNode.m_nodeCluster.m_nodes);
                    Debug.LogError("ERROR : No reachable nodes available in " + area.transform.parent.name + "/" + area.name + "  nodes:" + area.m_courseNode.m_nodeCluster.m_nodes.Count);
                }
                return true;
            }
            nodes = Utils.ToSystemList(area.m_courseNode.m_nodeCluster.m_nodes);
            return true;
        }

        public static AIG_INode GetClosestNode(List<AIG_INode> nodes, Vector3 pos)
        {
            if (nodes == null || nodes.Count <= 0)
                return null;
            AIG_INode node = nodes[0];
            float num = float.MaxValue;
            for (int index = 1; index < nodes.Count; ++index)
            {
                float sqrMagnitude = (nodes[index].Position - pos).sqrMagnitude;
                if ((double)sqrMagnitude < (double)num)
                {
                    node = nodes[index];
                    num = sqrMagnitude;
                }
            }
            return node;
        }

        public static bool TryGetNodePositionsFromTransforms(Il2CppSystem.Collections.Generic.List<Transform> transforms, LG_Area inArea, out List<Vector3> nodePositions)
        {
            if (transforms == null || transforms.Count <= 0)
            {
                nodePositions = null;
                return false;
            }
            List<AIG_INode> nodes;

            if (!TryGetNodes(inArea, true, out nodes))
            {
                nodePositions = null;
                return false;
            }
            nodePositions = new List<Vector3>();
            for (int index = 0; index < transforms.Count; ++index)
            {
                AIG_INode closestNode = GetClosestNode(nodes, transforms[index].position);
                if (closestNode != null)
                {
                    nodePositions.Add(closestNode.Position);
                }
            }

            if (inArea.m_geomorph.m_geoPrefab.name == "geo_64x64_mining_refinery_I_HA_01_v2") // remove that stupid duplicate scan point.
            {
                nodePositions.RemoveAt(0);
            }

            return true;
        }

        public static int ScanCount(ChainedPuzzleInstance CPInstance, int currentPuzzleIndex)
        {
            int count = 0;
            int bound = currentPuzzleIndex < CPInstance.m_chainedPuzzleCores.Count ? currentPuzzleIndex : CPInstance.m_chainedPuzzleCores.Count;
            for (int i = 0; i < bound; i++)
            {
                CP_Cluster_Core cluster_cp = CPInstance.m_chainedPuzzleCores[i].TryCast<CP_Cluster_Core>();
                if (cluster_cp == null) count += 1;
                else
                {
                    count += cluster_cp.m_amountOfPuzzles;
                }
            }

            return count;
        }

    }
}

