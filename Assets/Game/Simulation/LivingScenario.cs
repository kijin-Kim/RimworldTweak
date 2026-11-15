namespace RimworldTweak.Simulation
{
    public static class LivingScenario
    {
        public static ScenarioDefinition Create()
        {
            var initial = SandboxScenario.Create();
            var colonists = new ColonistSeed[initial.Colonists.Count];
            for (var i = 0; i < colonists.Length; i++)
                colonists[i] = new ColonistSeed(initial.Colonists[i].Id, initial.Colonists[i].Position, 3500, 2500);
            return new ScenarioDefinition(initial.Width, initial.Height, initial.BlockedTiles, colonists, initial.Resources,
                new[] { new BedSeed(201, new GridPosition(5, 5)), new BedSeed(202, new GridPosition(6, 5)) }, true);
        }
    }
}
