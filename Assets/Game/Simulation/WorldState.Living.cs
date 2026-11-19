using System.Collections.Generic;

namespace RimworldTweak.Simulation
{
    public sealed partial class WorldState
    {
        private readonly Dictionary<int, ResourceState> resourcesById = new Dictionary<int, ResourceState>();
        private readonly Dictionary<int, BedState> bedsById = new Dictionary<int, BedState>();
        private readonly List<LifeCommand> lifeCommands = new List<LifeCommand>();
        private readonly HashSet<int> cancelledThisTick = new HashSet<int>();
        private long availabilityRevision;
        public bool NeedsEnabled { get; }
        public IReadOnlyList<BedState> Beds { get; }
        private enum LifeCommandKind { SetResource, RemoveResource, SetBed, Cancel }
        private readonly struct LifeCommand
        {
            public LifeCommand(LifeCommandKind kind, int id, int amount = 0, bool exists = true, long actionId = 0)
            { Kind = kind; Id = id; Amount = amount; Exists = exists; ActionId = actionId; }
            public long ActionId { get; }
            public LifeCommandKind Kind { get; }
            public int Id { get; }
            public int Amount { get; }
            public bool Exists { get; }
        }
        private bool EnqueueLife(LifeCommand command)
        {
            lifeCommands.Add(command); LastCommandError = string.Empty; return true;
        }
        public bool QueueSetResourceAmount(int id, int amount)
        {
            if (!resourcesById.ContainsKey(id)) return Reject("InvalidResource");
            if (amount < 0 || resourcesById[id].StorageSlotId.HasValue && amount > StorageSlotState.Capacity) return Reject("InvalidAmount");
            return EnqueueLife(new LifeCommand(LifeCommandKind.SetResource, id, amount));
        }
        public bool QueueRemoveResource(int id)
        {
            if (!resourcesById.ContainsKey(id)) return Reject("InvalidResource");
            return EnqueueLife(new LifeCommand(LifeCommandKind.RemoveResource, id));
        }
        public bool QueueSetBedExists(int id, bool exists)
        {
            if (!bedsById.ContainsKey(id)) return Reject("InvalidBed");
            return EnqueueLife(new LifeCommand(LifeCommandKind.SetBed, id, exists: exists));
        }
        public bool QueueCancelLife(int id)
        {
            if (!colonistsById.TryGetValue(id, out var colonist)) return Reject("InvalidColonist");
            return QueueCancelLife(id, colonist.ActionId);
        }
        public bool QueueCancelLife(int id, long expectedActionId)
        {
            if (!colonistsById.TryGetValue(id, out var pawn)) return Reject("InvalidColonist");
            if (pawn.ActionId == expectedActionId)
            {
                pawn.HasSleepIntent = false;
                CancelFeatureWork(id, expectedActionId, WorkCancelReason.Explicit);
                pawn.SetFoodReason(string.Empty); pawn.SetSleepReason(string.Empty);
            }
            LastCommandError = string.Empty; return true;
        }
        private void ApplyLifeCommands()
        {
            foreach (var command in lifeCommands)
            {
                switch (command.Kind)
                {
                    case LifeCommandKind.SetResource:
                    case LifeCommandKind.RemoveResource:
                        var resource = resourcesById[command.Id];
                        var exists = command.Kind == LifeCommandKind.SetResource;
                        var amount = exists ? command.Amount : 0;
                        if (resource.Amount != amount || resource.Exists != exists)
                        { resource.SetAmount(amount, exists); availabilityRevision++; }
                        break;
                    case LifeCommandKind.SetBed:
                        var bed = bedsById[command.Id];
                        if (buildings.Find(b => b.Id == bed.Id && b.InitialPlacement) is BuildingState site)
                        {
                            if (!command.Exists && site.RepairDesignated) SetRepair(site.Id, false);
                            site.Exists = command.Exists;
                            bed.SetExists(command.Exists && site.Durability > 0);
                            availabilityRevision++;
                        }
                        else if (bed.Exists != command.Exists) { bed.SetExists(command.Exists); availabilityRevision++; }
                        break;
                    case LifeCommandKind.Cancel:
                        var cancelled = colonistsById[command.Id];
                        if (cancelled.ActionId == command.ActionId)
                        {
                            cancelled.HasSleepIntent = false;
                            EndLife(cancelled); cancelledThisTick.Add(command.Id);
                        }
                        break;
                }
            }
            lifeCommands.Clear();
        }
        private void EndLife(ColonistState colonist)
        {
            ReleaseEquipReservation(colonist.Id);
            CancelRescueForPawn(colonist.Id);
            foreach (var resource in Resources)
                if (resource.OwnerId == colonist.Id && resource.OwnerActionId == colonist.ActionId) { resource.Release(colonist.Id, colonist.ActionId); availabilityRevision++; }
            foreach (var bed in Beds)
                if (bed.OwnerId == colonist.Id && bed.OwnerActionId == colonist.ActionId) { bed.Release(colonist.Id, colonist.ActionId); availabilityRevision++; }
            ClearWork(colonist);
            colonist.Attack.Cancel();
            colonist.Stop(); colonist.ClearPending();
            moveCommands.RemoveAll(command => command.Id == colonist.Id);
            colonist.InvalidateAction();
            colonist.SetLife(LifeActivity.Idle);
            colonist.LastFoodRevision = -1;
            colonist.NextFoodSearchTick = 0;
            colonist.LastBedRevision = -1; colonist.NextBedSearchTick = 0;
        }
        private void ValidateLifeTargets()
        {
            foreach (var colonist in Colonists)
            {
                var activity = colonist.LifeActivity;
                if (activity == LifeActivity.GoingToFood || activity == LifeActivity.Eating)
                {
                    var resource = resourcesById[colonist.LifeTargetId.Value];
                    if (!resource.HasReservation(colonist.Id, colonist.ActionId, FoodAmount(resource)) ||
                        blockedSet.Contains(resource.Position) || colonist.Status == MovementStatus.Blocked)
                        EndLife(colonist);
                }
                else if (activity == LifeActivity.GoingToBed || activity == LifeActivity.SleepingInBed)
                {
                    var bed = bedsById[colonist.LifeTargetId.Value];
                    if (!bed.Exists || bed.OwnerId != colonist.Id || bed.OwnerActionId != colonist.ActionId || blockedSet.Contains(bed.Position) ||
                        colonist.Status == MovementStatus.Blocked)
                    {
                        EndLife(colonist);
                    }
                }
            }
        }
        private static int FoodAmount(ResourceState resource) => resource.Kind == ResourceKind.Meal ? 1 : 4;
        private static int FoodDuration(ResourceState resource) => resource.Kind == ResourceKind.Meal ? 20 : 40;
        private void UpdateNeedsAndActions()
        {
            foreach (var colonist in Colonists)
                if (!colonist.IsDowned) colonist.UpdateNeeds();
            InterruptWorkForNeeds();
            foreach (var colonist in Colonists)
            {
                if (colonist.LifeActivity == LifeActivity.Eating)
                {
                    colonist.AdvanceAction();
                    if (colonist.ActionProgress < colonist.ActionDuration) continue;
                    var resource = resourcesById[colonist.LifeTargetId.Value];
                    if (colonist.Position == resource.Position && resource.Consume(colonist.Id, colonist.ActionId, FoodAmount(resource)))
                    { colonist.CompleteMeal(resource.Kind == ResourceKind.Ingredients); availabilityRevision++; }
                    EndLife(colonist); colonist.SetFoodReason(string.Empty);
                }
                else if (colonist.IsSleeping)
                {
                    colonist.AdvanceAction();
                    if (colonist.Rest < 9000) continue;
                    colonist.CompleteSleep(); EndLife(colonist); colonist.SetSleepReason(string.Empty);
                }
            }
        }
        private void DecideLife()
        {
            foreach (var colonist in Colonists)
                if (!colonist.IsDowned && !colonist.IsDrafted && !cancelledThisTick.Contains(colonist.Id) && colonist.Rest <= 2000) colonist.HasSleepIntent = true;
            var order = new List<ColonistState>(Colonists);
            order.Sort((a, b) => { var need = a.Nutrition.CompareTo(b.Nutrition); return need != 0 ? need : a.Id.CompareTo(b.Id); });
            foreach (var colonist in order)
            {
                if (colonist.PatientClaim.OwnerId.HasValue && NeedsUrgentCareBreak(colonist)) CancelPatientTreatment(colonist);
                if (HasEquipOrder(colonist.Id) || HasRescueCommand(colonist.Id) || colonist.IsDowned || colonist.IsDrafted || cancelledThisTick.Contains(colonist.Id) ||
                    colonist.PatientClaim.OwnerId.HasValue) continue;
                if (colonist.Nutrition > 3000) { colonist.SetFoodReason(string.Empty); continue; }
                if (colonist.LifeActivity == LifeActivity.GoingToFood || colonist.LifeActivity == LifeActivity.Eating) continue;
                if (Tick < colonist.NextFoodSearchTick && colonist.LastFoodRevision == availabilityRevision &&
                    colonist.LastFoodPosition == colonist.Position) continue;
                var resource = ChooseFood(colonist, out var route, out var reason);
                if (resource == null)
                {
                    colonist.SetFoodReason(reason); colonist.NextFoodSearchTick = Tick + 10;
                    colonist.LastFoodRevision = availabilityRevision; colonist.LastFoodPosition = colonist.Position;
                    continue;
                }
                if (!resource.Reserve(colonist.Id, checked(colonist.ActionId + 1), FoodAmount(resource))) continue;
                EndLife(colonist);
                availabilityRevision++;
                colonist.SetLife(LifeActivity.GoingToFood, resource.Id, FoodDuration(resource));
                colonist.SetRoute(resource.Position, route);
                colonist.SetFoodReason(string.Empty); colonist.SetSleepReason(string.Empty);
            }
            order.Sort((a, b) => { var need = a.Rest.CompareTo(b.Rest); return need != 0 ? need : a.Id.CompareTo(b.Id); });
            foreach (var colonist in order)
            {
                if (colonist.PatientClaim.OwnerId.HasValue && NeedsUrgentCareBreak(colonist)) CancelPatientTreatment(colonist);
                if (HasEquipOrder(colonist.Id) || HasRescueCommand(colonist.Id) || colonist.IsDowned || colonist.IsDrafted || cancelledThisTick.Contains(colonist.Id) ||
                    colonist.PatientClaim.OwnerId.HasValue) continue;
                var floor = colonist.LifeActivity == LifeActivity.SleepingOnFloor;
                if (colonist.IsLifeBusy && !floor) continue;
                if (!colonist.HasSleepIntent && colonist.Rest > 2000) continue;
                if (colonist.Rest >= 9000) { colonist.HasSleepIntent = false; continue; }
                colonist.HasSleepIntent = true;
                if (floor && Tick < colonist.NextBedSearchTick && colonist.LastBedRevision == availabilityRevision && colonist.LastBedPosition == colonist.Position) continue;
                var bed = ChooseBed(colonist, out var route, out var reason);
                if (bed == null)
                {
                    if (!floor) { EndLife(colonist); colonist.SetLife(LifeActivity.SleepingOnFloor); }
                    colonist.SetSleepReason(reason);
                    colonist.NextBedSearchTick = Tick + 10; colonist.LastBedRevision = availabilityRevision; colonist.LastBedPosition = colonist.Position;
                    continue;
                }
                // Selection and acquisition occur in this single-threaded decision phase.
                // Do not interrupt a floor sleeper until a usable replacement has been found.
                if (!bed.Reserve(colonist.Id, checked(colonist.ActionId + 1))) continue;
                EndLife(colonist);
                availabilityRevision++;
                colonist.SetSleepReason(string.Empty);
                colonist.SetLife(LifeActivity.GoingToBed, bed.Id); colonist.SetRoute(bed.Position, route);
            }
        }
        private ResourceState ChooseFood(ColonistState colonist, out IReadOnlyList<GridPosition> route, out string reason)
        {
            route = null;
            var anyTotal = false; var anyAvailable = false;
            foreach (var kind in new[] { ResourceKind.Meal, ResourceKind.Ingredients })
            {
                ResourceState best = null;
                foreach (var resource in Resources)
                {
                    if (!resource.Exists || resource.Kind != kind || resource.Amount < FoodAmount(resource)) continue;
                    anyTotal = true;
                    if (resource.AccessibleAmount < FoodAmount(resource)) continue;
                    anyAvailable = true;
                    var candidate = FindPath(colonist.Position, resource.Position);
                    if (candidate == null) continue;
                    if (best == null || candidate.Count < route.Count || (candidate.Count == route.Count && resource.Id < best.Id))
                    { best = resource; route = candidate; }
                }
                if (best != null) { reason = string.Empty; return best; }
            }
            reason = anyAvailable ? "FoodUnreachable" : anyTotal ? "FoodReserved" : "NoFood";
            return null;
        }
        private BedState ChooseBed(ColonistState colonist, out IReadOnlyList<GridPosition> route, out string reason)
        {
            route = null; BedState best = null; var any = false; var available = false;
            foreach (var bed in Beds)
            {
                if (!bed.Exists) continue;
                any = true;
                if (bed.OwnerId.HasValue) continue;
                available = true;
                var candidate = FindPath(colonist.Position, bed.Position);
                if (candidate == null) continue;
                if (best == null || candidate.Count < route.Count || (candidate.Count == route.Count && bed.Id < best.Id))
                { best = bed; route = candidate; }
            }
            reason = best != null ? string.Empty : !any ? "NoBed" : available ? "BedUnreachable" : "BedReserved";
            return best;
        }
        private void HandleLifeArrivals()
        {
            foreach (var colonist in Colonists)
            {
                if (colonist.Status != MovementStatus.Arrived) continue;
                if (colonist.LifeActivity == LifeActivity.GoingToFood)
                {
                    var target = resourcesById[colonist.LifeTargetId.Value];
                    if (!target.HasReservation(colonist.Id, colonist.ActionId, FoodAmount(target)) || colonist.Position != target.Position) { EndLife(colonist); continue; }
                    target.Occupy(colonist.Id, colonist.ActionId);
                    colonist.SetLife(LifeActivity.Eating, target.Id, FoodDuration(target));
                }
                else if (colonist.LifeActivity == LifeActivity.GoingToBed)
                {
                    var target = bedsById[colonist.LifeTargetId.Value];
                    if (!target.Exists || target.OwnerId != colonist.Id || target.OwnerActionId != colonist.ActionId || colonist.Position != target.Position) { EndLife(colonist); continue; }
                    target.Occupy(colonist.Id, colonist.ActionId); colonist.SetLife(LifeActivity.SleepingInBed, target.Id);
                }
            }
        }
    }
}
