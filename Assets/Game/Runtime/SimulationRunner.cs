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

        private void Update()
        {
            // Synchronous file I/O must not become catch-up simulation time in the next frame.
            AdvanceTime((decimal)Time.unscaledDeltaTime);
        }

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

        private bool PlayerCommand(Func<bool> command)
        {
            return command();
        }

        public bool MoveColonist(int id, GridPosition target) => PlayerCommand(() => Session.World.QueueMove(id, target));
        public bool DesignateGather(int id, bool designated) => PlayerCommand(() => Session.World.QueueDesignateGather(id, designated));
        public bool CreateStorage(GridPosition first, GridPosition second) => PlayerCommand(() => Session.World.QueueCreateStorage(first, second));
        public bool SetStorageFilter(int id, ResourceKind kind, bool allowed) => PlayerCommand(() => Session.World.QueueStorageFilter(id, kind, allowed));
        public bool DeleteStorage(int id) => PlayerCommand(() => Session.World.QueueDeleteStorage(id));
        public bool PlaceBlueprint(BuildingKind kind, GridPosition position) => PlayerCommand(() => Session.World.PlaceBlueprint(kind,position));
        public bool CancelBlueprint(int id) => PlayerCommand(() => Session.World.CancelBlueprint(id));
        public bool SetDemolition(int id,bool enabled) => PlayerCommand(() => Session.World.SetDemolition(id,enabled));
        public bool CreateGrowingZone(GridPosition first,GridPosition second) => PlayerCommand(() => Session.World.CreateGrowingZone(first,second));
        public bool SetSowingEnabled(int id,bool enabled) => PlayerCommand(() => Session.World.SetSowingEnabled(id,enabled));
        public bool DeleteGrowingZone(int id) => PlayerCommand(() => Session.World.DeleteGrowingZone(id));
        public bool ToggleDraft(int id) => PlayerCommand(() => Session.World.ToggleDraft(id));
    }
}
