namespace RimworldTweak.Simulation
{
    public static class RaidScenario
    {
        public static ScenarioDefinition Create() => new ScenarioDefinition(
            16, 12, new[] { new GridPosition(10, 8), new GridPosition(10, 9) },
            new[] {
                new ColonistSeed(1, new GridPosition(8, 5)),
                new ColonistSeed(2, new GridPosition(8, 6)),
                new ColonistSeed(3, new GridPosition(6, 5))
            },
            new[] {
                new ResourceSeed(101, ResourceKind.Meal, 36, new GridPosition(2, 3)),
                new ResourceSeed(102, ResourceKind.Wood, 40, new GridPosition(3, 3)),
                new ResourceSeed(103, ResourceKind.Ingredients, 20, new GridPosition(3, 4)),
                new ResourceSeed(104, ResourceKind.Medicine, 12, new GridPosition(3, 5))
            },
            new[] {
                new BedSeed(201, new GridPosition(2, 5)),
                new BedSeed(202, new GridPosition(2, 6)),
                new BedSeed(203, new GridPosition(2, 7))
            }, true, initialRaidStove: new GridPosition(5, 5));
    }
}
