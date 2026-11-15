using System;

namespace RimworldTweak.Simulation
{
    public sealed class SimulationSession
    {
        private const decimal SecondsPerTick = 0.1m;
        private readonly ScenarioDefinition scenario;

        public SimulationSession(ScenarioDefinition scenario)
        {
            World = WorldState.Create(scenario);
            this.scenario = scenario;
        }

        public WorldState World { get; private set; }
        public int ResetCount { get; private set; }
        public decimal PendingSeconds { get; private set; }

        public void AdvanceTime(decimal seconds)
        {
            if (seconds < 0m) throw new ArgumentOutOfRangeException(nameof(seconds));

            var totalSeconds = checked(PendingSeconds + seconds);
            var tickCount = checked((long)decimal.Floor(totalSeconds / SecondsPerTick));
            var remainder = totalSeconds - tickCount * SecondsPerTick;

            // Keep both the tick and remainder unchanged if the tick would overflow.
            World.Step(tickCount);
            PendingSeconds = remainder;
        }

        public void Step(long count)
        {
            World.Step(count);
        }

        public void Reset()
        {
            var nextResetCount = checked(ResetCount + 1);
            var newWorld = WorldState.Create(scenario);
            World = newWorld;
            PendingSeconds = 0m;
            ResetCount = nextResetCount;
        }
    }
}
