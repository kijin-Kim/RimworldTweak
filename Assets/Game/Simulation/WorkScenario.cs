namespace RimworldTweak.Simulation
{
    public static class WorkScenario
    {
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
