using System.Collections.Generic;
using System.Linq;
using ChainedPuzzles;
using GTFO.API;
using Il2CppSystem.Text;

namespace ScanPosOverride.Managers
{
    public class PuzzleReqItemManager
    {
        public static readonly PuzzleReqItemManager Current;

        private Dictionary<int, CarryItemPickup_Core> BigPickupItemsInLevel = new();

        private int itemIndexCounter = 1;

        private List<(CP_Bioscan_Core, List<int>)> bioscanCoresToAddReqItems = new();
        private List<(CP_Cluster_Core, List<int>)> clusterCoresToAddReqItems = new();

        // core.PlayerScanner -> core
        private Dictionary<System.IntPtr, CP_Bioscan_Core> movableScansWithReqItems = new();

        internal int Register(CarryItemPickup_Core item)
        {
            int allotedIndex = itemIndexCounter;
            itemIndexCounter += 1;

            BigPickupItemsInLevel.Add(allotedIndex, item);
            return allotedIndex;
        }

        internal void QueueForAddingReqItems(CP_Bioscan_Core core, List<int> itemsIndices) => bioscanCoresToAddReqItems.Add((core, itemsIndices));

        internal void QueueForAddingReqItems(CP_Cluster_Core core, List<int> itemsIndices) => clusterCoresToAddReqItems.Add((core, itemsIndices));

        internal CP_Bioscan_Core GetMovableCoreWithReqItem(CP_PlayerScanner scanner) => movableScansWithReqItems.ContainsKey(scanner.Pointer) ? movableScansWithReqItems[scanner.Pointer] : null; 

        public CarryItemPickup_Core GetBigPickupItem(int bigPickupInLevelIndex) => BigPickupItemsInLevel.ContainsKey(bigPickupInLevelIndex) ? BigPickupItemsInLevel[bigPickupInLevelIndex] : null;

        public bool AddReqItems(CP_Bioscan_Core puzzle, int itemIndex)
        {
            // Issue: cannot detect duplicate added items.
            // User is now responsible for not adding duplicate.
            if (puzzle == null) return false;

            if (!BigPickupItemsInLevel.ContainsKey(itemIndex))
            {
                Logger.Error($"Unregistered BigPickup Item with index {itemIndex}");
                return false;
            }

            CarryItemPickup_Core carryItemPickup_Core = BigPickupItemsInLevel[itemIndex];
            puzzle.AddRequiredItems(new iWardenObjectiveItem[1] { new iWardenObjectiveItem(carryItemPickup_Core.Pointer) });

            return true;
        }

        public bool AddReqItems(CP_Cluster_Core puzzle, int itemIndex)
        {
            // Issue: cannot detect duplicate added items.
            // User is now responsible for not adding duplicate.
            if (puzzle == null) return false;

            if (!BigPickupItemsInLevel.ContainsKey(itemIndex))
            {
                Logger.Error($"Unregistered BigPickup Item with index {itemIndex}");
                return false;
            }

            CarryItemPickup_Core carryItemPickup_Core = BigPickupItemsInLevel[itemIndex];

            foreach(var childCore in puzzle.m_childCores)
            {
                childCore.AddRequiredItems(new iWardenObjectiveItem[1] { new iWardenObjectiveItem(carryItemPickup_Core.Pointer) });
            }

            return true;
        }

        public bool AddReqItems(CP_Bioscan_Core puzzle, List<int> itemsIndices)
        {
            if (puzzle == null || itemsIndices == null || itemsIndices.Count < 1) return false;

            bool addedReqItem = false;

            foreach (int itemIndex in itemsIndices.ToHashSet())
            {
                addedReqItem |= AddReqItems(puzzle, itemIndex);
            }

            if(puzzle.IsMovable && addedReqItem)
            {
                movableScansWithReqItems.Add(puzzle.m_playerScanner.Pointer, puzzle);
            }

            return addedReqItem;
        }

        public void RemoveReqItem(CP_Bioscan_Core puzzle, int itemIndex)
        {
            if (puzzle == null) return;

            if (!BigPickupItemsInLevel.ContainsKey(itemIndex))
            {
                Logger.Error($"Unregistered BigPickup Item with index {itemIndex}");
                return;
            }

            CarryItemPickup_Core carryItemPickup_Core = BigPickupItemsInLevel[itemIndex];

            // accidentally find this function.
            // untested
            puzzle.RemoveRequiredItems(new iWardenObjectiveItem[1] { new iWardenObjectiveItem(carryItemPickup_Core.Pointer) });
        }

        public void RemoveReqItem(CP_Cluster_Core puzzle, int itemIndex)
        {
            if (puzzle == null) return;

            if (!BigPickupItemsInLevel.ContainsKey(itemIndex))
            {
                Logger.Error($"Unregistered BigPickup Item with index {itemIndex}");
                return;
            }

            CarryItemPickup_Core carryItemPickup_Core = BigPickupItemsInLevel[itemIndex];

            foreach(var childCore in puzzle.m_childCores)
            {
                childCore.RemoveRequiredItems(new iWardenObjectiveItem[1] { new iWardenObjectiveItem(carryItemPickup_Core.Pointer) });
            }
        }

        private void AddQueuedReqItems()
        {
            foreach (var tuple in bioscanCoresToAddReqItems)
            {
                CP_Bioscan_Core core = tuple.Item1;
                List<int> itemsIndices = tuple.Item2;

                AddReqItems(core, itemsIndices);
            }

            foreach (var tuple in clusterCoresToAddReqItems)
            {
                CP_Cluster_Core core = tuple.Item1;
                List<int> itemsIndices = tuple.Item2;

                foreach (var childCore in core.m_childCores)
                {
                    CP_Bioscan_Core bioscan_Core = childCore.TryCast<CP_Bioscan_Core>();
                    if (bioscan_Core == null)
                    {
                        Logger.Error("Failed to cast child core to CP_Bioscan_Core");
                        continue;
                    }

                    AddReqItems(bioscan_Core, itemsIndices);
                }
            }
        }

        public void OutputLevelBigPickupInfo()
        {
            StringBuilder info = new();
            info.AppendLine();
            List<CarryItemPickup_Core> allBigPickups = new(BigPickupItemsInLevel.Values);

            allBigPickups.Sort((b1, b2) => {
                var n1 = b1.SpawnNode;
                var n2 = b2.SpawnNode;

                if (n1.m_dimension.DimensionIndex != n2.m_dimension.DimensionIndex)
                    return (int)n1.m_dimension.DimensionIndex <= (int)n2.m_dimension.DimensionIndex ? -1 : 1;

                if(n1.LayerType != n2.LayerType) 
                    return (int)n1.LayerType < (int)n2.LayerType ? -1 : 1;

                if(n1.m_zone.LocalIndex != n2.m_zone.LocalIndex) 
                    return (int)n1.m_zone.LocalIndex < (int)n2.m_zone.LocalIndex ? -1 : 1;

                return 0;
            });

            Dictionary<CarryItemPickup_Core, int> itemIndicesInLevel = new();
            foreach(int itemIndex in BigPickupItemsInLevel.Keys)
                itemIndicesInLevel.Add(BigPickupItemsInLevel[itemIndex], itemIndex);
            

            foreach (CarryItemPickup_Core item in allBigPickups)
            {
                info.AppendLine($"Item Name: {item.ItemDataBlock.publicName}");
                info.AppendLine($"Zone {item.SpawnNode.m_zone.Alias}, {item.SpawnNode.LayerType}, Dim {item.SpawnNode.m_dimension.DimensionIndex}");
                info.AppendLine($"Item Index: {itemIndicesInLevel[item]}");
                info.AppendLine();
            }

            Logger.Debug(info.ToString());
        }

        internal void OnEnterLevel()
        {
            AddQueuedReqItems();
            OutputLevelBigPickupInfo();
        }

        public void Clear()
        {
            BigPickupItemsInLevel.Clear();
            itemIndexCounter = 1;
            bioscanCoresToAddReqItems.Clear();
            clusterCoresToAddReqItems.Clear();
            movableScansWithReqItems.Clear();
        }

        static PuzzleReqItemManager()
        {
            Current = new();
            LevelAPI.OnLevelCleanup += Current.Clear;
            LevelAPI.OnEnterLevel += Current.OnEnterLevel;
        }

        private PuzzleReqItemManager() { }
    }
}
