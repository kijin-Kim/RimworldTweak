namespace RimworldTweak.Simulation
{
    public static class SandboxScenario
    {
        public static ScenarioDefinition Create()
        {
            return new ScenarioDefinition(
                8,
                8,
                new[] { new GridPosition(3, 3) },
                new[]
                {
                    new ColonistSeed(1, new GridPosition(1, 1)),
                    new ColonistSeed(2, new GridPosition(2, 1)),
                    new ColonistSeed(3, new GridPosition(1, 2))
                },
                new[]
                {
                    new ResourceSeed(101, ResourceKind.Wood, 40, new GridPosition(4, 1)),
                    new ResourceSeed(102, ResourceKind.Meal, 18, new GridPosition(4, 2))
                });
        }
    }
}
