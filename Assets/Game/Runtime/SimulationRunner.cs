using System;
using RimworldTweak.Simulation;
using UnityEngine;

namespace RimworldTweak.Runtime
{
    public sealed class SimulationRunner : MonoBehaviour
    {
        public SimulationSession Session { get; private set; }
        public bool IsPaused { get; private set; }
        public int SpeedMultiplier { get; private set; } = 1;

        private void Awake()
        {
            if (Session == null) Initialize(WorkScenario.Create());
        }

        private void Update() => AdvanceTime((decimal)Time.unscaledDeltaTime);

        public void Initialize(ScenarioDefinition scenario)
        {
            Session = new SimulationSession(scenario);
            IsPaused = false;
            SpeedMultiplier = 1;
        }

        public void AdvanceTime(decimal realSeconds)
        {
            if (realSeconds < 0m) throw new ArgumentOutOfRangeException(nameof(realSeconds));
            if (!IsPaused) Session.AdvanceTime(checked(realSeconds * SpeedMultiplier));
        }

        public void SetSpeed(int multiplier)
        {
            if (multiplier != 1 && multiplier != 3) throw new ArgumentOutOfRangeException(nameof(multiplier));
            SpeedMultiplier = multiplier;
            IsPaused = false;
        }

        public void SetPaused(bool paused) => IsPaused = paused;
        public void ResetWorld() => Session.Reset();
        public void StepOnce()
        {
            if (IsPaused) Session.Step(1);
        }

        public bool MoveColonist(int id, GridPosition target) => Session.World.QueueMove(id, target);
        public bool DesignateGather(int id, bool designated) => Session.World.QueueDesignateGather(id, designated);
        public bool CreateStorage(GridPosition first, GridPosition second) => Session.World.QueueCreateStorage(first, second);
        public bool SetStorageFilter(int id, ResourceKind kind, bool allowed) => Session.World.QueueStorageFilter(id, kind, allowed);
        public bool DeleteStorage(int id) => Session.World.QueueDeleteStorage(id);
        public bool ToggleDraft(int id) => Session.World.ToggleDraft(id);
    }
}
