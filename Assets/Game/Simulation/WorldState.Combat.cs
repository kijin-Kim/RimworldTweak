using System;
using System.Collections.Generic;
using System.Linq;

namespace RimworldTweak.Simulation
{
    public enum RaidPhase { Idle, Warning, Active, Retreating, Resolved }
    public enum RaidOutcome { None, Defended, ObjectiveLost, TimeLimit, AllDown }

    public sealed class AttackState
    {
        public int? TargetId { get; internal set; }
        public int Preparation { get; internal set; }
        public int Cooldown { get; internal set; }
        public string BlockedReason { get; internal set; } = string.Empty;

        internal void Cancel()
        {
            TargetId = null;
            Preparation = 0;
            BlockedReason = string.Empty;
        }

        internal bool Advance(bool valid, int aimTicks = 10, int recoveryTicks = 10)
        {
            if (!valid) Preparation = 0;
            if (Cooldown > 0)
            {
                Cooldown--;
                return false;
            }
            if (!valid || ++Preparation < aimTicks) return false;
            Preparation = 0;
            Cooldown = recoveryTicks;
            return true;
        }
    }

    public sealed class RaiderState
    {
        internal RaiderState(int id, GridPosition position) { Id = id; Position = position; }
        public int Id { get; }
        public GridPosition Position { get; internal set; }
        public int Health { get; internal set; } = 50;
        public AttackState Attack { get; } = new AttackState();
        internal GridPosition? NextCell { get; set; }
        internal int MoveProgress { get; set; }
        internal long RetryTick { get; set; }
        internal long RouteRevision { get; set; } = -1;
    }

    public sealed partial class WorldState
    {
        public const int RaidWarningTicks = 100;
        public const int RaidCombatTicks = 1200;
        public const int RaidRetreatTicks = 100;
        private readonly List<RaiderState> raiders = new List<RaiderState>();
        private readonly HashSet<int> raidInjured = new HashSet<int>();
        private readonly HashSet<int> raidDowned = new HashSet<int>();
        public IReadOnlyList<RaiderState> Raiders => raiders.AsReadOnly();
        public bool RaidTrial { get; private set; }
        public RaidPhase RaidPhase { get; private set; }
        public RaidOutcome RaidOutcome { get; private set; }
        public int RaidRemaining { get; private set; }
        public int? RaidObjectiveId { get; private set; }
        public int RaidInjuredCount => raidInjured.Count;
        public int RaidDownedCount => raidDowned.Count;
        public int RaidFacilityDamage { get; private set; }
        public int RaidForcedRetreats { get; private set; }
        public bool RaidDefeatStopsTime => RaidOutcome == RaidOutcome.AllDown;
        public GridPosition RaidEntrance(int index) => new GridPosition(Width - 1, index == 0 ? Height / 2 - 1 : Height / 2);
        private bool RaidRunning => RaidPhase == RaidPhase.Warning || RaidPhase == RaidPhase.Active || RaidPhase == RaidPhase.Retreating;
        private bool IsRaidEntrance(GridPosition cell) => RaidRunning && (cell == RaidEntrance(0) || cell == RaidEntrance(1));

        public bool StartRaid(int objectiveId)
        {
            var objective = Building(objectiveId);
            if (RaidRunning || Colonists.Count == 0 || Colonists.Any(pawn => pawn.Health < 100))
                return Reject("RaidNotReady");
            if (objective == null || objective.Kind != BuildingKind.Stove || objective.Durability < 100 || objective.DemolitionDesignated)
                return Reject("InvalidRaidObjective");
            if (!RaidEntrancesAvailable()) return Reject("RaidEntranceBlocked");
            BeginRaid(objectiveId);
            return true;
        }

        public const int DefaultRetargetPercent = 30;
        public bool NormalRaid { get; private set; }
        internal int RetargetPercent { get; private set; }
        internal uint RetargetState { get; private set; }

        public bool StartNormalRaid(int retargetPercent = DefaultRetargetPercent)
        {
            if (RaidRunning || !Colonists.Any(p => !p.IsDowned)) return Reject("RaidNotReady");
            if (retargetPercent < 0 || retargetPercent > 100) return Reject("InvalidRaidProbability");
            if (!RaidEntrancesAvailable()) return Reject("RaidEntranceBlocked");
            BeginRaid(null);
            RetargetPercent = retargetPercent;
            RetargetState = unchecked((uint)Tick * 2654435761u + (uint)nextEntityId + 1u) | 1u;
            for (var index = 0; index < 2; index++)
            {
                var raider = new RaiderState(NewId(), RaidEntrance(index));
                ChooseNearestColonist(raider);
                raiders.Add(raider);
            }
            return true;
        }

        private bool RaidEntrancesAvailable()
        {
            for (var index = 0; index < 2; index++)
            {
                var cell = RaidEntrance(index);
                if (!InBounds(cell) || blockedSet.Contains(cell) ||
                    blueprints.Any(b => b.Exists && b.Position == cell && ConstructionRules.BlocksMovement(b.Kind)) ||
                    topologyCommands.Any(command => command.Position == cell && command.Blocked))
                    return false;
            }
            return true;
        }

        private void BeginRaid(int? objectiveId)
        {
            NormalRaid = !objectiveId.HasValue;
            RaidObjectiveId = objectiveId;
            RaidPhase = NormalRaid ? RaidPhase.Active : RaidPhase.Warning;
            RaidRemaining = NormalRaid ? RaidCombatTicks : RaidWarningTicks;
            RaidOutcome = RaidOutcome.None;
            raidInjured.Clear();
            raidDowned.Clear();
            RaidFacilityDamage = 0;
            RaidForcedRetreats = 0;
            LastCommandError = string.Empty;
        }

        private void ChooseNearestColonist(RaiderState raider)
        {
            ColonistState nearest = null;
            var bestDistance = int.MaxValue;
            foreach (var pawn in Colonists)
            {
                if (pawn.IsDowned) continue;
                var distance = TileDistance(raider.Position, pawn.Position);
                if (distance > bestDistance || (distance == bestDistance && nearest != null && pawn.Id >= nearest.Id)) continue;
                nearest = pawn;
                bestDistance = distance;
            }
            if (raider.Attack.TargetId == nearest?.Id) return;
            raider.Attack.Cancel();
            raider.Attack.TargetId = nearest?.Id;
            raider.RetryTick = 0;
        }

        private bool RollRetarget()
        {
            var state = RetargetState;
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            RetargetState = state;
            return (state * 100UL >> 32) < (ulong)RetargetPercent;
        }

        public bool OrderAttack(int colonistId, int targetId)
        {
            if (!colonistsById.TryGetValue(colonistId, out var pawn) || pawn.IsDowned || !pawn.IsDrafted)
                return Reject("AttackRequiresDraft");
            if (RaidPhase != RaidPhase.Active || !raiders.Exists(target => target.Id == targetId))
                return Reject("InvalidAttackTarget");
            if (EquipmentEnabled && EquippedWeapon(colonistId) == null) return Reject("WeaponRequired");
            StopDirectOrders(colonistId);
            if (pawn.Attack.TargetId != targetId)
            {
                EndLife(pawn);
                pawn.Attack.TargetId = targetId;
            }
            LastCommandError = string.Empty;
            return true;
        }

        private static int TileDistance(GridPosition a, GridPosition b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

        private bool BlocksShot(GridPosition cell)
        {
            var building = buildings.Find(b => b.Exists && b.Durability > 0 && b.Position == cell);
            if (building != null) return building.Kind == BuildingKind.Wall || building.Kind == BuildingKind.Door;
            return blockedSet.Contains(cell);
        }

        private bool HasClearShot(GridPosition from, GridPosition to, int range)
        {
            return from == to || SightRules.Evaluate(from, to, range, cell => new SightCell(BlocksShot(cell), 0)).CanAttack;
        }

        private void TickCombat()
        {
            if (RaidPhase == RaidPhase.Warning)
            {
                foreach (var pawn in Colonists) pawn.Attack.Advance(false);
                if (--RaidRemaining > 0) return;
                for (var index = 0; index < 2; index++) raiders.Add(new RaiderState(NewId(), RaidEntrance(index)));
                RaidPhase = RaidPhase.Active;
                RaidRemaining = RaidCombatTicks;
                return;
            }
            if (RaidPhase != RaidPhase.Active)
            {
                foreach (var pawn in Colonists) pawn.Attack.Advance(false);
                if (RaidPhase == RaidPhase.Retreating) AdvanceRetreat();
                return;
            }

            var damage = new SortedDictionary<int, int>();
            foreach (var raider in raiders) AdvanceRaider(raider, damage);
            foreach (var pawn in Colonists)
            {
                var attack = pawn.Attack;
                var target = raiders.Find(x => x.Id == attack.TargetId);
                var weapon = EquipmentEnabled ? EquippedWeapon(pawn.Id)?.Definition : null;
                var range = weapon?.Range ?? 4;
                var reason = target == null || pawn.IsDowned || !pawn.IsDrafted ? "InvalidAttackTarget" :
                    EquipmentEnabled && weapon == null ? "WeaponRequired" :
                    TileDistance(pawn.Position, target.Position) > range ? "AttackOutOfRange" :
                    weapon?.Kind != WeaponKind.Melee && !HasClearShot(pawn.Position, target.Position, range) ? "AttackBlocked" : string.Empty;
                attack.BlockedReason = attack.TargetId.HasValue ? reason : string.Empty;
                if (attack.Advance(reason.Length == 0, weapon?.AimTicks ?? 10, weapon?.RecoveryTicks ?? 10))
                    AddDamage(damage, target.Id, ArmorRules.Resolve(new DamageInput(weapon?.Damage ?? 25), null).RemainingDamage);
            }

            ResolveCombatDamage(damage);
        }

        internal void ResolveCombatDamage(SortedDictionary<int, int> damage)
        {
            foreach (var hit in damage) ApplyCombatDamage(hit.Key, hit.Value);
            raiders.RemoveAll(raider => raider.Health == 0);
            foreach (var pawn in Colonists)
                if (pawn.Attack.TargetId.HasValue && !raiders.Exists(x => x.Id == pawn.Attack.TargetId)) pawn.Attack.Cancel();
            RaidRemaining--;
            if (Colonists.All(pawn => pawn.IsDowned)) EndRaid(RaidOutcome.AllDown, false);
            else if (RaidOutcome == RaidOutcome.ObjectiveLost) EndRaid(RaidOutcome.ObjectiveLost, true);
            else if (raiders.Count == 0) EndRaid(RaidOutcome.Defended, false);
            else if (RaidRemaining == 0) EndRaid(RaidOutcome.TimeLimit, true);
            else if (NormalRaid)
            {
                // Raiders are spawned and restored in ID order for stable draw ordering.
                foreach (var raider in raiders)
                {
                    var valid = raider.Attack.TargetId is int id &&
                        colonistsById.TryGetValue(id, out var target) && !target.IsDowned;
                    if (!valid || (damage.TryGetValue(raider.Id, out var amount) && amount > 0 && RollRetarget()))
                        ChooseNearestColonist(raider);
                }
            }
        }

        internal static void AddDamage(IDictionary<int, int> damage, int id, int amount)
        {
            damage.TryGetValue(id, out var current);
            damage[id] = current + amount;
        }

        private IReadOnlyList<GridPosition> ReadApproachPath(GridPosition start, GridPosition target)
        {
            IReadOnlyList<GridPosition> best = null;
            foreach (var offset in Directions)
            {
                var path = ReadPath(start, new GridPosition(target.X + offset.X, target.Y + offset.Y));
                if (path != null && (best == null || path.Count < best.Count)) best = path;
            }
            return best;
        }

        private void AdvanceRaider(RaiderState raider, IDictionary<int, int> damage)
        {
            if (NormalRaid)
            {
                var attack = raider.Attack;
                IReadOnlyList<GridPosition> pursuit = null;
                if (attack.TargetId is int pursuedId && colonistsById.TryGetValue(pursuedId, out var pursuedPawn) && !pursuedPawn.IsDowned)
                    pursuit = ReadApproachPath(raider.Position, pursuedPawn.Position);
                attack.BlockedReason = pursuit == null ? "Unreachable" : string.Empty;
                if (attack.Advance(pursuit != null && pursuit.Count == 0)) AddDamage(damage, attack.TargetId.Value, 10);
                AdvanceRaiderMove(raider, pursuit);
                return;
            }

            int? target = null;
            IReadOnlyList<GridPosition> path = null;
            if (raider.RetryTick <= Tick || raider.RouteRevision != MapRevision)
            {
                foreach (var pawn in Colonists)
                {
                    if (pawn.IsDowned) continue;
                    var approach = ReadApproachPath(raider.Position, pawn.Position);
                    if (approach == null || (path != null && approach.Count >= path.Count)) continue;
                    path = approach;
                    target = pawn.Id;
                }
                if (path == null && RaidObjectiveId is int id && Building(id) is BuildingState objective)
                {
                    path = ReadApproachPath(raider.Position, objective.Position);
                    if (path != null) target = id;
                }
                raider.RouteRevision = MapRevision;
                if (path == null) raider.RetryTick = Tick + 10;
            }
            var adjacent = path != null && path.Count == 0;
            if (raider.Attack.TargetId != target) raider.Attack.Cancel();
            raider.Attack.TargetId = target;
            raider.Attack.BlockedReason = path == null ? "Unreachable" : string.Empty;
            if (raider.Attack.Advance(adjacent)) AddDamage(damage, target.Value, 10);
            AdvanceRaiderMove(raider, path);
        }

        private static void AdvanceRaiderMove(RaiderState raider, IReadOnlyList<GridPosition> path)
        {
            GridPosition? next = path != null && path.Count > 0 ? path[0] : null;
            if (raider.NextCell != next) raider.MoveProgress = 0;
            raider.NextCell = next;
            if (!next.HasValue || ++raider.MoveProgress < 5) return;
            raider.Position = next.Value;
            raider.NextCell = null;
            raider.MoveProgress = 0;
        }

        private void EndRaid(RaidOutcome outcome, bool retreat)
        {
            RaidOutcome = outcome;
            foreach (var pawn in Colonists) pawn.Attack.Cancel();
            foreach (var raider in raiders) raider.Attack.Cancel();
            RaidPhase = retreat && raiders.Count > 0 ? RaidPhase.Retreating : RaidPhase.Resolved;
            RaidRemaining = RaidPhase == RaidPhase.Retreating ? RaidRetreatTicks : 0;
            if (RaidPhase == RaidPhase.Resolved) raiders.Clear();
        }

        private void AdvanceRetreat()
        {
            foreach (var raider in raiders)
            {
                var first = ReadPath(raider.Position, RaidEntrance(0));
                var second = ReadPath(raider.Position, RaidEntrance(1));
                AdvanceRaiderMove(raider, first == null || (second != null && second.Count < first.Count) ? second : first);
            }
            raiders.RemoveAll(raider => raider.Position == RaidEntrance(0) || raider.Position == RaidEntrance(1));
            if (--RaidRemaining > 0 && raiders.Count > 0) return;
            RaidForcedRetreats = raiders.Count;
            raiders.Clear();
            RaidRemaining = 0;
            RaidPhase = RaidPhase.Resolved;
        }

        internal void ApplyCombatDamage(int id, int amount)
        {
            if (colonistsById.TryGetValue(id, out var pawn))
            {
                CancelPatientTreatment(pawn);
                if (pawn.FeatureStep == WorkStep.Care) FinishProduction(pawn);
                pawn.Health = Math.Max(0, pawn.Health - amount);
                raidInjured.Add(id);
                if (!pawn.IsDowned) return;
                raidDowned.Add(id);
                StopDirectOrders(id);
                pawn.IsDrafted = false;
                pawn.HasSleepIntent = false;
                EndLife(pawn);
                availabilityRevision++;
                return;
            }
            var raider = raiders.Find(x => x.Id == id);
            if (raider != null) { raider.Health = Math.Max(0, raider.Health - amount); return; }
            var building = Building(id);
            if (building == null || building.Durability == 0) return;
            var applied = Math.Min(building.Durability, amount);
            building.Durability -= applied;
            RaidFacilityDamage += applied;
            if (building.Durability != 0) return;
            foreach (var worker in Colonists)
                if (!IsRecovery(worker.FeatureStep) && (worker.WorkTargetId == id || worker.LifeTargetId == id ||
                    worker.PendingDeliveryTargetId == id)) FinishProduction(worker);
            if (building.Kind == BuildingKind.Bed)
            {
                bedsById[id].SetExists(false);
                InvalidateRescueBed(id);
            }
            if (ConstructionRules.BlocksMovement(building.Kind)) SetProductionBlocked(building.Position, false);
            if (RaidObjectiveId == id) RaidOutcome = RaidOutcome.ObjectiveLost;
            availabilityRevision++;
        }
    }
}
