namespace RimworldTweak.Simulation
{
    public static class WorkScenario
    {
        public static ScenarioDefinition CreateTestMap()
        {
            var initial = Create();
            var gatherables = new System.Collections.Generic.List<GatherSeed>(initial.Gatherables);
            var id = 305;
            for (var y = 8; y < 30; y += 5)
                for (var x = 8; x < 30; x += 5)
                    gatherables.Add(new GatherSeed(id++, (x + y) % 2 == 0 ? GatherKind.Tree : GatherKind.EdiblePlant, new GridPosition(x, y)));
            return new ScenarioDefinition(32, 32, initial.BlockedTiles, initial.Colonists, initial.Resources,
                initial.Beds, initial.NeedsEnabled, gatherables);
        }

        public static ScenarioDefinition Create()
        {
            var living = LivingScenario.Create();
            return new ScenarioDefinition(living.Width, living.Height, living.BlockedTiles, living.Colonists, living.Resources,
                living.Beds, true, new[] {
                    new GatherSeed(301, GatherKind.Tree, new GridPosition(1, 5)),
                    new GatherSeed(302, GatherKind.Tree, new GridPosition(2, 6)),
                    new GatherSeed(303, GatherKind.EdiblePlant, new GridPosition(4, 6)),
                    new GatherSeed(304, GatherKind.EdiblePlant, new GridPosition(6, 3))
                });
        }
    }
}
