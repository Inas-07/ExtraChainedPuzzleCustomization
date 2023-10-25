using ExtraObjectiveSetup.BaseClasses;
using ScanPosOverride.PuzzleOverrideData;
using System.Collections.Generic;
namespace ScanPosOverride.Managers
{
    public class PuzzleDefinitionManager : InstanceDefinitionManager<PuzzleInstanceDefinition>
    {
        public static PuzzleDefinitionManager Current { private set; get; } = new();

        protected override string DEFINITION_NAME => "ChainedPuzzles";

        protected override void AddDefinitions(InstanceDefinitionsForLevel<PuzzleInstanceDefinition> definitions)
        {
            // because we have chained puzzles, sorting is necessary to preserve chained puzzle instance order.
            Sort(definitions);
            base.AddDefinitions(definitions);
        }

        private PuzzleDefinitionManager() : base() { }

        static PuzzleDefinitionManager() { }

        public static PuzzleInstanceDefinition From(PuzzleOverride oldDef)
        {
            var result = new PuzzleInstanceDefinition()
            {
                Index = oldDef.Index,
                Position = oldDef.Position,
                Rotation = oldDef.Rotation,
                HideSpline = oldDef.HideSpline,
                ConcurrentCluster = oldDef.ConcurrentCluster,
                TMoveSpeedMulti = oldDef.TMoveSpeedMulti,
                TPositions = oldDef.TPositions,
                RequiredItemsIndices = oldDef.RequiredItemsIndices,
                EventsOnPuzzleActivate = oldDef.EventsOnPuzzleActivate,
                EventsOnPuzzleSolved = oldDef.EventsOnPuzzleSolved,
            };

            return result;
        }
    }
}
