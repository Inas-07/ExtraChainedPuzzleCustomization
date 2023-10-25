using ExtraObjectiveSetup.BaseClasses;
using ScanPosOverride.PuzzleOverrideData;

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
    }
}
