using System;
using System.Collections.Generic;

namespace RimworldTweak.Simulation
{
    public sealed partial class WorldState
    {
        private bool advancingTick;
        private readonly List<GridPosition> blockedTiles;
        private readonly HashSet<GridPosition> blockedSet;
        private GridPathSearch gridSearch;
        private SparsePathSearch sparseSearch;
        private GridPathSearch directedGridSearch;
        private SparsePathSearch directedSparseSearch;
        private readonly Dictionary<int, ColonistState> colonistsById = new Dictionary<int, ColonistState>();
        private readonly List<MoveCommand> moveCommands = new List<MoveCommand>();
        private readonly List<TopologyCommand> topologyCommands = new List<TopologyCommand>();
        private static readonly GridPosition[] Directions = {
            new GridPosition(1, 0), new GridPosition(0, 1),
            new GridPosition(-1, 0), new GridPosition(0, -1)
        };

        private readonly struct MoveCommand
        {
            public MoveCommand(int id, GridPosition? destination) { Id = id; Destination = destination; }
            public int Id { get; }
            public GridPosition? Destination { get; }
        }

        private readonly struct TopologyCommand
        {
            public TopologyCommand(GridPosition position, bool blocked) { Position = position; Blocked = blocked; }
            public GridPosition Position { get; }
            public bool Blocked { get; }
        }

        private WorldState(int width, int height, List<GridPosition> blockedTiles,
            List<ColonistState> colonists, List<ResourceState> resources, List<BedState> beds, bool needsEnabled)
        {
            NeedsEnabled = needsEnabled;
            mutableBeds = beds; Beds = beds.AsReadOnly();
            foreach (var resource in resources) resourcesById.Add(resource.Id, resource);
            foreach (var bed in beds) bedsById.Add(bed.Id, bed);
            Width = width;
            Height = height;
            this.blockedTiles = blockedTiles;
            blockedSet = new HashSet<GridPosition>(blockedTiles);
            BlockedTiles = blockedTiles.AsReadOnly();
            Colonists = colonists.AsReadOnly();
            Resources = resources.AsReadOnly();
            mutableResources = resources;
            foreach (var colonist in colonists) { colonistsById.Add(colonist.Id, colonist); workPolicies.Add(new WorkPolicyState(colonist.Id)); }
        }

        public int Width { get; }
        public int Height { get; }
        public long Tick { get; private set; }
        public IReadOnlyList<GridPosition> BlockedTiles { get; }
        public IReadOnlyList<ColonistState> Colonists { get; }
        public IReadOnlyList<ResourceState> Resources { get; }
        public long MapRevision { get; private set; }
        public long PathSearchCount { get; private set; }
        public int PendingCommandCount => moveCommands.Count + topologyCommands.Count + lifeCommands.Count;
        public string LastCommandError { get; private set; } = string.Empty;

        public bool QueueMove(int colonistId, GridPosition destination)
        {
            if (!colonistsById.TryGetValue(colonistId, out var colonist)) return Reject("InvalidColonist");
            if (colonist.IsDowned) return Reject("ColonistDowned");
            if (colonist.IsWorking) return Reject("WorkBusy");
            if (NeedsEnabled && colonist.IsLifeBusy) return Reject("LifeBusy");
            if (!colonist.IsDrafted) return Reject("NotDrafted");
            if (!InBounds(destination)) return Reject("OutOfBounds");
            if (blockedSet.Contains(destination)) return Reject("BlockedDestination");
            StopDirectOrders(colonistId);
            colonist.Attack.Cancel();
            moveCommands.Add(new MoveCommand(colonistId, destination));
            colonist.MarkPending(destination);
            LastCommandError = string.Empty;
            return true;
        }

        // Player control is immediate, including while the simulation clock is paused.
        public bool QueueStop(int colonistId) => SetDrafted(colonistId, true);

        private void StopDirectOrders(int pawnId)
        {
            ReleaseEquipReservation(pawnId);
            ReleasePriorityOrder(pawnId);
            CancelRescueForPawn(pawnId);
        }

        public bool CancelDirectOrder(int pawnId)
        {
            if(!colonistsById.TryGetValue(pawnId,out var pawn)) return Reject("InvalidColonist");
            if(!HasEquipOrder(pawnId) && !HasRescueCommand(pawnId) && !PriorityWorkTarget(pawnId).HasValue)
                return Reject("NoDirectOrder");
            StopDirectOrders(pawnId);
            EndLife(pawn);
            LastCommandError=string.Empty;
            return true;
        }

        public bool ToggleDraft(int colonistId)
        {
            if (!colonistsById.TryGetValue(colonistId, out var colonist)) return Reject("InvalidColonist");
            return SetDrafted(colonistId, !colonist.IsDrafted);
        }

        public bool SetDrafted(int colonistId, bool drafted)
        {
            if (!colonistsById.TryGetValue(colonistId, out var colonist)) return Reject("InvalidColonist");
            if (colonist.IsDowned) return Reject("ColonistDowned");
            if (drafted) CancelPatientTreatment(colonist);
            var wasDrafted = colonist.IsDrafted;
            colonist.IsDrafted = drafted;
            if (drafted || wasDrafted)
            {
                StopDirectOrders(colonistId);
                colonist.HasSleepIntent = false;
                EndLife(colonist);
                lifeCommands.RemoveAll(command => command.Kind == LifeCommandKind.Cancel && command.Id == colonistId);
                colonist.SetFoodReason(string.Empty);
                colonist.SetSleepReason(string.Empty);
            }
            LastCommandError = string.Empty;
            return true;
        }

        public bool QueueSetBlocked(GridPosition position, bool blocked)
        {
            if (!InBounds(position)) return Reject("OutOfBounds");
            if (blocked && IsRaidEntrance(position)) return Reject("RaidEntranceBlocked");
            if (blocked && IsOccupied(position)) return Reject("OccupiedTile");
            topologyCommands.Add(new TopologyCommand(position, blocked));
            LastCommandError = string.Empty;
            return true;
        }

        private bool Reject(string code)
        {
            LastCommandError = code;
            return false;
        }

        private bool InBounds(GridPosition position) =>
            position.X >= 0 && position.Y >= 0 && position.X < Width && position.Y < Height;

        private bool IsOccupied(GridPosition position)
        {
            foreach (var colonist in Colonists)
                if (colonist.Position == position) return true;
            return raiders.Exists(raider => raider.Position == position);
        }

        // Equal-cost shortest path. Queue and neighbor order make ties deterministic.
        public IReadOnlyList<GridPosition> FindPath(GridPosition start, GridPosition destination)
        {
            PathSearchCount++;
            return ReadPath(start,destination);
        }

        private int FindPathCost(GridPosition start, GridPosition destination, out int steps)
        {
            PathSearchCount++;
            steps = 0;
            if (!HasPath(start, destination)) return -1;
            if (start == destination) return 0;
            steps = gridSearch != null ? gridSearch.StepsTo(destination) : sparseSearch.StepsTo(destination);
            return gridSearch != null ? gridSearch.Cost(destination) : sparseSearch.Cost(destination);
        }

        private IReadOnlyList<GridPosition> ReadPath(GridPosition start, GridPosition destination)
        {
            bool directed = Math.Max(Math.Abs((long)destination.X - start.X), Math.Abs((long)destination.Y - start.Y)) >= 24;
            if (!HasPath(start, destination, directed)) return null;
            if (start == destination) return Array.Empty<GridPosition>();
            var grid = directed ? directedGridSearch : gridSearch;
            return grid != null ? grid.BuildRoute(destination) : (directed ? directedSparseSearch : sparseSearch).BuildRoute(destination);
        }

        private bool HasPath(GridPosition start, GridPosition destination, bool directed = false)
        {
            if (!InBounds(start) || !InBounds(destination) || blockedSet.Contains(start) || blockedSet.Contains(destination))
                return false;
            if (start == destination) return true;
            if (directed && !HasOpenNeighbor(destination)) return false;
            if (Width <= WorkScenario.SettlementSize && Height <= WorkScenario.SettlementSize)
            {
                ref var search = ref (directed ? ref directedGridSearch : ref gridSearch);
                if (search == null) search = new GridPathSearch(Width, Height, directed);
                return search.Reaches(start, destination, MapRevision, blockedTiles);
            }
            ref var sparse = ref (directed ? ref directedSparseSearch : ref sparseSearch);
            if (sparse == null) sparse = new SparsePathSearch(this, directed);
            return sparse.Reaches(start, destination);
        }

        // A diagonal also requires an open cardinal neighbor at the endpoint.
        private bool HasOpenNeighbor(GridPosition cell)
        {
            foreach (var direction in Directions)
            {
                var neighbor = new GridPosition(cell.X + direction.X, cell.Y + direction.Y);
                if (InBounds(neighbor) && !blockedSet.Contains(neighbor)) return true;
            }
            return false;
        }

        private bool CanWalkStep(GridPosition from, GridPosition to)
        {
            if (!InBounds(from) || !InBounds(to) || blockedSet.Contains(to)) return false;
            var dx = Math.Abs(to.X - from.X);
            var dy = Math.Abs(to.Y - from.Y);
            if (dx > 1 || dy > 1 || dx + dy == 0) return false;
            return dx == 0 || dy == 0 || (!blockedSet.Contains(new GridPosition(from.X, to.Y)) &&
                !blockedSet.Contains(new GridPosition(to.X, from.Y)));
        }

        public void Step(long count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            // Check before consuming any queued command or mutating movement.
            _ = checked(Tick + count);
            for (long i = 0; i < count; i++)
            {
                if (RaidDefeatStopsTime) break;
                advancingTick = true;
                try
                {
                    cancelledThisTick.Clear();
                    ApplyLifeCommands();
                    ApplyTopologyCommands();
                    GrowCrops();
                    ReconcileCookingCycles(true);
                    ValidateWork();
                    if (NeedsEnabled) { ValidateLifeTargets(); UpdateNeedsAndActions(); }
                    if (NeedsEnabled) DecideLife();
                    ProgressFeatureWork();
                    DecideWork();
                    ApplyMoveCommands();
                    foreach (var colonist in Colonists) colonist.AdvanceMovement(NeedsEnabled && colonist.Nutrition == 0);
                    FinishEquipArrivals();
                    AdvanceRescues();
                    if (NeedsEnabled) HandleLifeArrivals();
                    HandleFeatureArrivals();
                    TickCombat();
                    if (!RaidDefeatStopsTime) ProgressRecovery();
                    Tick++;
                }
                finally { advancingTick = false; }
            }
        }

        private void ApplyTopologyCommands()
        {
            var changed = false;
            foreach (var command in topologyCommands)
            {
                if (command.Blocked && (IsOccupied(command.Position) || IsRaidEntrance(command.Position))) { Reject("OccupiedTile"); continue; }
                var applied = command.Blocked ? blockedSet.Add(command.Position) : blockedSet.Remove(command.Position);
                if (applied)
                {
                    changed = true;
                    MapRevision++;
                }
                LastCommandError = string.Empty;
            }
            topologyCommands.Clear();
            if (!changed) return;
            RefreshRaiderSteps();
            availabilityRevision++;
            blockedTiles.Clear();
            blockedTiles.AddRange(blockedSet);
            blockedTiles.Sort((left, right) => {
                var byY = left.Y.CompareTo(right.Y);
                return byY != 0 ? byY : left.X.CompareTo(right.X);
            });
            foreach (var colonist in Colonists)
            {
                if ((colonist.Status == MovementStatus.Moving || colonist.Status == MovementStatus.Blocked) &&
                    colonist.Destination.HasValue)
                {
                    var target = colonist.Destination.Value;
                    colonist.SetRoute(target, FindPath(colonist.Position, target));
                }
            }
        }

        private void ApplyMoveCommands()
        {
            foreach (var command in moveCommands)
            {
                var colonist = colonistsById[command.Id];
                if (!colonist.IsDrafted) { Reject("NotDrafted"); continue; }
                if (colonist.IsWorking) { Reject("WorkBusy"); continue; }
                if (NeedsEnabled && colonist.IsLifeBusy) { Reject("LifeBusy"); continue; }
                if (!command.Destination.HasValue)
                {
                    colonist.Stop();
                    LastCommandError = string.Empty;
                    continue;
                }
                var destination = command.Destination.Value;
                if (blockedSet.Contains(destination)) { Reject("BlockedDestination"); continue; }
                if (colonist.Status == MovementStatus.Moving && colonist.Destination == destination)
                {
                    LastCommandError = string.Empty;
                    continue;
                }
                var path = FindPath(colonist.Position, destination);
                if (path == null) { Reject("Unreachable"); continue; }
                colonist.SetRoute(destination, path);
                LastCommandError = string.Empty;
            }
            moveCommands.Clear();
            foreach (var colonist in Colonists) colonist.ClearPending();
        }

        public static WorldState Create(ScenarioDefinition scenario)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            if (scenario.Width <= 0 || scenario.Height <= 0)
            {
                throw new ScenarioValidationException("InvalidDimensions", "Map width and height must be positive.");
            }

            var blockedTiles = new List<GridPosition>();
            var blockedSet = new HashSet<GridPosition>();
            foreach (var position in scenario.BlockedTiles)
            {
                ValidatePosition(position, scenario);
                if (!blockedSet.Add(position))
                {
                    throw new ScenarioValidationException("DuplicateBlockedTile", $"Blocked tile {position} is repeated.");
                }
                blockedTiles.Add(position);
            }

            var ids = new HashSet<int>();
            var colonists = new List<ColonistState>();
            foreach (var colonist in scenario.Colonists)
            {
                if (colonist == null)
                {
                    throw new ScenarioValidationException("NullEntity", "A colonist entry is null.");
                }
                ValidateEntity(colonist.Id, colonist.Position, scenario, ids, blockedSet);
                if (colonist.Nutrition < 0 || colonist.Nutrition > 10000 || colonist.Rest < 0 || colonist.Rest > 10000)
                    throw new ScenarioValidationException("InvalidNeeds", "Initial needs must be between 0 and 10000.");
                colonists.Add(new ColonistState(colonist.Id, colonist.Position, colonist.Nutrition, colonist.Rest));
            }

            var resources = new List<ResourceState>();
            foreach (var resource in scenario.Resources)
            {
                if (resource == null)
                {
                    throw new ScenarioValidationException("NullEntity", "A resource entry is null.");
                }
                ValidateEntity(resource.Id, resource.Position, scenario, ids, blockedSet);
                if (resource.Amount <= 0)
                {
                    throw new ScenarioValidationException("InvalidAmount", $"Resource {resource.Id} must have a positive amount.");
                }
                if (!Enum.IsDefined(typeof(ResourceKind), resource.Kind))
                {
                    throw new ScenarioValidationException("InvalidResourceKind", $"Resource {resource.Id} has an unknown kind.");
                }
                resources.Add(new ResourceState(resource));
            }

            blockedTiles.Sort((left, right) =>
            {
                var byY = left.Y.CompareTo(right.Y);
                return byY != 0 ? byY : left.X.CompareTo(right.X);
            });
            colonists.Sort((left, right) => left.Id.CompareTo(right.Id));
            resources.Sort((left, right) => left.Id.CompareTo(right.Id));

            var beds = new List<BedState>();
            foreach (var bed in scenario.Beds)
            {
                if (bed == null) throw new ScenarioValidationException("NullEntity", "A bed entry is null.");
                ValidateEntity(bed.Id, bed.Position, scenario, ids, blockedSet);
                beds.Add(new BedState(bed));
            }
            beds.Sort((left, right) => left.Id.CompareTo(right.Id));
            var nodes = new List<GatherState>();
            foreach (var seed in scenario.Gatherables)
            {
                if (seed == null) throw new ScenarioValidationException("NullEntity", "A gather target is null.");
                ValidateEntity(seed.Id, seed.Position, scenario, ids, blockedSet);
                if (!Enum.IsDefined(typeof(GatherKind), seed.Kind)) throw new ScenarioValidationException("InvalidGatherKind", "Unknown gather kind.");
                nodes.Add(new GatherState(seed));
            }
            nodes.Sort((a, b) => a.Id.CompareTo(b.Id));
            var world = new WorldState(scenario.Width, scenario.Height, blockedTiles, colonists, resources, beds, scenario.NeedsEnabled);
            world.gatherables.AddRange(nodes);
            foreach (var id in ids) world.nextEntityId = Math.Max(world.nextEntityId, id);
            foreach (var bed in beds)
                world.buildings.Add(new BuildingState(bed.Id, BuildingKind.Bed, bed.Position) { InitialPlacement = true });
            if (scenario.InitialRaidStove is GridPosition stoveCell)
            {
                if (!world.CanPlaceProduction(stoveCell) || !world.CanPlaceProduction(ConstructionRules.ServiceCell(stoveCell)))
                    throw new ScenarioValidationException("InvalidRaidStove", "The trial stove and its service tile must be free.");
                var stove = new BuildingState(world.NewId(), BuildingKind.Stove, stoveCell) { InitialPlacement = true };
                world.buildings.Add(stove);
                world.SetProductionBlocked(stoveCell, true);
                world.InitializeStove(stove);
                world.RaidTrial = true;
            }
            if(scenario.StartWithWeapons) world.SeedStartingWeapons();
            return world;
        }

        private static void ValidatePosition(GridPosition position, ScenarioDefinition scenario)
        {
            if (position.X < 0 || position.Y < 0 || position.X >= scenario.Width || position.Y >= scenario.Height)
            {
                throw new ScenarioValidationException("OutOfBounds", $"Position {position} is outside the map.");
            }
        }

        private static void ValidateEntity(
            int id,
            GridPosition position,
            ScenarioDefinition scenario,
            HashSet<int> ids,
            HashSet<GridPosition> blockedTiles)
        {
            if (id <= 0)
            {
                throw new ScenarioValidationException("InvalidId", "Entity IDs must be positive.");
            }
            if (!ids.Add(id))
            {
                throw new ScenarioValidationException("DuplicateId", $"Entity ID {id} is repeated.");
            }
            ValidatePosition(position, scenario);
            if (blockedTiles.Contains(position))
            {
                throw new ScenarioValidationException("BlockedPosition", $"Entity {id} occupies blocked tile {position}.");
            }
        }
    }
}
