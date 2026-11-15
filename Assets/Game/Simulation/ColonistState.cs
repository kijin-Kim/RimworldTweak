using System;
using System.Collections.Generic;
using Activity = RimworldTweak.Simulation.LifeActivity;

namespace RimworldTweak.Simulation
{
    public enum MovementStatus { Idle, Moving, Arrived, Blocked }
    public enum LifeActivity { Idle, GoingToFood, Eating, GoingToBed, SleepingInBed, SleepingOnFloor }

    public sealed partial class ColonistState
    {
        private readonly List<GridPosition> path = new List<GridPosition>();
        internal ColonistState(int id, GridPosition position, int nutrition = 10000, int rest = 10000)
        {
            Id = id; Position = position; Nutrition = nutrition; Rest = rest; Path = path.AsReadOnly();
        }
        public int Id { get; }
        public GridPosition Position { get; private set; }
        public GridPosition? Destination { get; private set; }
        public IReadOnlyList<GridPosition> Path { get; }
        public int MovementProgress { get; private set; }
        public int MovementTicksPerTile { get; private set; } = 5;
        public MovementStatus Status { get; private set; }
        public bool HasPendingCommand { get; private set; }
        public GridPosition? PendingDestination { get; private set; }
        public bool IsDrafted { get; internal set; }
        public long ActionId { get; private set; }
        public bool HasSleepIntent { get; internal set; }
        public WorkActivity WorkActivity { get; internal set; }
        public int? WorkTargetId { get; internal set; }
        public WorkStep? FeatureStep { get; internal set; }
        public int? PendingDeliveryTargetId { get; internal set; }
        public ResourceKind CargoKind { get; internal set; }
        public int CargoAmount { get; internal set; }
        public string WorkReason { get; internal set; } = string.Empty;
        internal long WorkReadyTick { get; set; }
        internal long NextWorkSearchTick { get; set; }
        internal long LastWorkRevision { get; set; } = -1;
        public bool IsWorking => WorkActivity != WorkActivity.Idle;

        internal long NextBedSearchTick { get; set; }
        internal long LastBedRevision { get; set; } = -1;
        internal GridPosition LastBedPosition { get; set; }
        internal void InvalidateAction() => ActionId = checked(ActionId + 1);
        public int Nutrition { get; private set; }
        public int Rest { get; private set; }
        public LifeActivity LifeActivity { get; private set; }
        public int? LifeTargetId { get; private set; }
        public int ActionProgress { get; private set; }
        public int ActionDuration { get; private set; }
        public string FoodReason { get; private set; } = string.Empty;
        public string SleepReason { get; private set; } = string.Empty;
        public string LifeReason => Nutrition == 0 ? "Starving" : !string.IsNullOrEmpty(FoodReason) ? FoodReason : SleepReason;
        public int MealsEaten { get; private set; }
        public int RawMealsEaten { get; private set; }
        public int SleepsCompleted { get; private set; }
        public long StarvationTicks { get; private set; }
        internal bool IsSleeping => LifeActivity == Activity.SleepingInBed || LifeActivity == Activity.SleepingOnFloor;
        internal bool IsLifeBusy => LifeActivity != Activity.Idle;
        internal long NextFoodSearchTick { get; set; }
        internal long LastFoodRevision { get; set; } = -1;
        internal GridPosition LastFoodPosition { get; set; }

        internal void MarkPending(GridPosition? destination) { HasPendingCommand = true; PendingDestination = destination; }
        internal void ClearPending() { HasPendingCommand = false; PendingDestination = null; }
        internal void SetRoute(GridPosition destination, IReadOnlyList<GridPosition> route)
        {
            Destination = destination; path.Clear();
            if (route != null) for (var i = 0; i < route.Count; i++) path.Add(route[i]);
            MovementProgress = 0; MovementTicksPerTile = 5;
            Status = route == null ? MovementStatus.Blocked : path.Count == 0 ? MovementStatus.Arrived : MovementStatus.Moving;
        }
        internal void Stop()
        {
            Destination = null; path.Clear(); MovementProgress = 0; MovementTicksPerTile = 5; Status = MovementStatus.Idle;
        }
        internal void AdvanceMovement(bool starving = false)
        {
            if (Status != MovementStatus.Moving) return;
            if (MovementProgress == 0) MovementTicksPerTile = starving ? 10 : 5;
            MovementProgress++;
            if (MovementProgress < MovementTicksPerTile) return;
            Position = path[0]; path.RemoveAt(0); MovementProgress = 0;
            if (path.Count == 0) Status = MovementStatus.Arrived;
        }
        internal void UpdateNeeds()
        {
            Nutrition = Math.Max(0, Nutrition - 10);
            Rest = IsSleeping ? Math.Min(10000, Rest + (LifeActivity == Activity.SleepingInBed ? 50 : 25)) : Math.Max(0, Rest - 5);
            if (Nutrition == 0) StarvationTicks++;
        }
        internal void SetLife(Activity activity, int? target = null, int duration = 0)
        {
            LifeActivity = activity; LifeTargetId = target; ActionProgress = 0; ActionDuration = duration;
        }
        internal void AdvanceAction() => ActionProgress++;
        internal void CompleteMeal(bool raw)
        {
            Nutrition = Math.Min(10000, Nutrition + 6000);
            if (raw) RawMealsEaten++; else MealsEaten++;
        }
        internal void CompleteSleep() { SleepsCompleted++; HasSleepIntent = false; }
        internal void SetFoodReason(string reason) => FoodReason = reason;
        internal void SetSleepReason(string reason) => SleepReason = reason;
    }
}
