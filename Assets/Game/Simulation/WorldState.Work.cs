using System;
using System.Collections.Generic;

namespace RimworldTweak.Simulation
{
    public sealed partial class WorldState
    {
        private readonly List<GatherState> gatherables = new List<GatherState>();
        private readonly List<StorageZoneState> storageZones = new List<StorageZoneState>();
        private List<ResourceState> mutableResources;
        private int nextEntityId;
        private long requestSequence;
        public IReadOnlyList<GatherState> Gatherables => gatherables.AsReadOnly();
        public IReadOnlyList<StorageZoneState> StorageZones => storageZones.AsReadOnly();
        private readonly Dictionary<ResourceKind, int> produced = new Dictionary<ResourceKind, int>();
        public int ProducedAmount(ResourceKind kind) => produced.TryGetValue(kind, out var value) ? value : 0;
        public int CargoTotal(ResourceKind kind)
        {
            var total = 0; foreach (var pawn in Colonists) if (pawn.CargoKind == kind) total += pawn.CargoAmount; return total;
        }
        public int ResourceTotal(ResourceKind kind, bool stored)
        {
            var total = 0;
            foreach (var item in Resources) if (item.Exists && item.Kind == kind && item.StorageSlotId.HasValue == stored) total += item.Amount;
            return total;
        }
        private int NewId() => checked(++nextEntityId);
        private GatherState Gather(int id) => gatherables.Find(item => item.Id == id);
        private StorageZoneState Zone(int id) => storageZones.Find(item => item.Id == id && item.Exists);
        private StorageSlotState Slot(int id)
        {
            foreach (var zone in storageZones) if (zone.Exists)
                foreach (var slot in zone.Slots) if (slot.Id == id) return slot;
            return null;
        }
        public bool QueueDesignateGather(int id, bool designated)
        {
            var target = Gather(id);
            if (target == null || target.Depleted) return Reject("InvalidGatherTarget");
            if (target.Designated == designated) { LastCommandError = string.Empty; return true; }
            target.Designated = designated;
            if (designated) target.RequestOrder = ++requestSequence;
            else foreach (var pawn in Colonists)
                if ((pawn.WorkActivity == WorkActivity.GoingToGather || pawn.WorkActivity == WorkActivity.Gathering) && pawn.WorkTargetId == id)
                { EndLife(pawn); cancelledThisTick.Add(pawn.Id); }
            availabilityRevision++;
            LastCommandError = string.Empty; return true;
        }
        public bool QueueCreateStorage(GridPosition first, GridPosition second)
        {
            if (!ValidStorageRectangle(first, second)) return Reject("InvalidStorageArea");
            var zoneId = NewId(); var slots = new List<StorageSlotState>();
            for (var y = Math.Min(first.Y, second.Y); y <= Math.Max(first.Y, second.Y); y++)
                for (var x = Math.Min(first.X, second.X); x <= Math.Max(first.X, second.X); x++)
                    slots.Add(new StorageSlotState(NewId(), zoneId, new GridPosition(x, y)));
            storageZones.Add(new StorageZoneState(zoneId, slots)); availabilityRevision++;
            LastCommandError = string.Empty; return true;
        }
        private bool ValidStorageRectangle(GridPosition first, GridPosition second)
        {
            if (!InBounds(first) || !InBounds(second)) return false;
            for (var y = Math.Min(first.Y, second.Y); y <= Math.Max(first.Y, second.Y); y++)
                for (var x = Math.Min(first.X, second.X); x <= Math.Max(first.X, second.X); x++)
                {
                    var position = new GridPosition(x, y);
                    if (blockedSet.Contains(position)) return false;
                    foreach (var bed in Beds) if (bed.Exists && bed.Position == position) return false;
                    foreach (var node in gatherables) if (!node.Depleted && node.Position == position) return false;
                    foreach (var zone in storageZones) if (zone.Exists)
                        foreach (var slot in zone.Slots) if (slot.Position == position) return false;
                }
            return true;
        }
        public bool QueueStorageFilter(int zoneId, ResourceKind kind, bool allowed)
        {
            if (Zone(zoneId) == null || !Enum.IsDefined(typeof(ResourceKind), kind)) return Reject("InvalidStorageFilter");
            var zone = Zone(zoneId);
            zone.SetFilter(kind, allowed); RefreshStorage(zone); CancelUnavailablePickups(); availabilityRevision++;
            LastCommandError = string.Empty; return true;
        }
        public bool QueueDeleteStorage(int zoneId)
        {
            if (Zone(zoneId) == null) return Reject("InvalidStorageZone");
            var zone = Zone(zoneId);
            // Keep the zone discoverable until all active owners have been cancelled.
            foreach (var slot in zone.Slots) CancelSlotOwners(slot);
            zone.Exists = false; RefreshStorage(zone); CancelUnavailablePickups(); availabilityRevision++;
            LastCommandError = string.Empty; return true;
        }
        private void CancelSlotOwners(StorageSlotState slot)
        {
            if (slot.OwnerId.HasValue && colonistsById.TryGetValue(slot.OwnerId.Value, out var pawn))
            { EndLife(pawn); cancelledThisTick.Add(pawn.Id); }
        }
        private void RefreshStorage(StorageZoneState zone)
        {
            foreach (var slot in zone.Slots)
            {
                if (slot.OwnerId.HasValue && colonistsById.TryGetValue(slot.OwnerId.Value, out var pawn) &&
                    pawn.CargoAmount > 0 && !zone.Allows(pawn.CargoKind)) CancelSlotOwners(slot);
                if (slot.Stack != null && !zone.Allows(slot.Stack.Kind))
                {
                    CancelSlotOwners(slot);
                    slot.Stack.DetachStorage(); slot.Stack = null;
                }
            }
        }
        private ResourceState SpawnResource(ResourceKind kind, int amount, GridPosition position)
        {
            var item = new ResourceState(new ResourceSeed(NewId(), kind, amount, position));
            item.CreatedTick = advancingTick ? Tick : Tick - 1;
            mutableResources.Add(item); resourcesById.Add(item.Id, item); availabilityRevision++; return item;
        }
        private void ClearWork(ColonistState pawn)
        {
            foreach (var node in gatherables) node.Claim.Release(pawn.Id, pawn.ActionId);
            foreach (var zone in storageZones) foreach (var slot in zone.Slots) slot.Claim.Release(pawn.Id, pawn.ActionId);
            if (pawn.CargoAmount > 0)
            {
                SpawnResource(pawn.CargoKind, pawn.CargoAmount, pawn.Position); pawn.CargoAmount = 0;
            }
            if (pawn.IsWorking) availabilityRevision++;
            pawn.WorkActivity = WorkActivity.Idle; pawn.WorkTargetId = null; pawn.WorkReason = string.Empty;
            pawn.LastWorkRevision = -1; pawn.NextWorkSearchTick = 0;
        }

        private void CancelUnavailablePickups()
        {
            foreach (var pawn in Colonists)
                if (pawn.WorkActivity == WorkActivity.GoingToPickup || pawn.WorkActivity == WorkActivity.PickingUp)
                {
                    var source = resourcesById[pawn.WorkTargetId.Value];
                    if (ChooseStorage(source.Kind, source.Position, out _) == null) EndLife(pawn);
                }
        }
        private void ValidateWork()
        {
            foreach (var pawn in Colonists)
            {
                if (!pawn.IsWorking) continue;
                var valid = !pawn.IsDrafted && pawn.Status != MovementStatus.Blocked;
                if (pawn.WorkActivity == WorkActivity.GoingToGather || pawn.WorkActivity == WorkActivity.Gathering)
                {
                    var node = Gather(pawn.WorkTargetId.Value);
                    valid &= node != null && node.Designated && !node.Depleted && node.Claim.Matches(pawn.Id, pawn.ActionId) && !blockedSet.Contains(node.Position);
                }
                else if (pawn.WorkActivity == WorkActivity.GoingToPickup || pawn.WorkActivity == WorkActivity.PickingUp)
                {
                    var source = resourcesById[pawn.WorkTargetId.Value];
                    valid &= source.Exists && source.Amount > 0 && source.Claim.Matches(pawn.Id, pawn.ActionId) && !blockedSet.Contains(source.Position);
                }
                else
                {
                    var slot = Slot(pawn.WorkTargetId.Value);
                    valid &= slot != null && slot.Claim.Matches(pawn.Id, pawn.ActionId) && !blockedSet.Contains(slot.Position) &&
                        Zone(slot.ZoneId).Allows(pawn.CargoKind) && slot.Amount + pawn.CargoAmount <= StorageSlotState.Capacity;
                }
                if (!valid) { EndLife(pawn); cancelledThisTick.Add(pawn.Id); }
            }
        }
        private bool HasFoodAfterWorkCancellation(ColonistState pawn)
        {
            if (pawn.CargoKind == ResourceKind.Meal && pawn.CargoAmount >= 1 || pawn.CargoKind == ResourceKind.Ingredients && pawn.CargoAmount >= 4) return true;
            foreach (var item in Resources)
                if (item.Exists && item.Kind != ResourceKind.Wood && item.Amount >= FoodAmount(item) &&
                    (!item.OwnerId.HasValue || item.Claim.Matches(pawn.Id, pawn.ActionId)) && FindPath(pawn.Position, item.Position) != null) return true;
            return false;
        }
        private void InterruptWorkForNeeds()
        {
            foreach (var pawn in Colonists)
                if (pawn.IsWorking && !pawn.IsDrafted && (pawn.Rest <= 2000 || pawn.HasSleepIntent ||
                    pawn.Nutrition <= 3000 && HasFoodAfterWorkCancellation(pawn))) EndLife(pawn);
        }
        private StorageSlotState ChooseStorage(ResourceKind kind, GridPosition start, out IReadOnlyList<GridPosition> route)
        {
            StorageSlotState best = null; route = null;
            foreach (var zone in storageZones)
            {
                if (!zone.Allows(kind)) continue;
                foreach (var slot in zone.Slots)
                {
                    if (slot.OwnerId.HasValue || slot.Amount >= StorageSlotState.Capacity || slot.Amount > 0 && slot.Stack.Kind != kind) continue;
                    var path = FindPath(start, slot.Position);
                    if (path == null) continue;
                    if (best == null || path.Count < route.Count) { best = slot; route = path; }
                }
            }
            return best;
        }
        private void DecideWork()
        {
            if (gatherables.Count == 0 && storageZones.Count == 0) return;
            foreach (var pawn in Colonists)
            {
                if (pawn.IsDrafted || pawn.IsLifeBusy || pawn.IsWorking || pawn.HasSleepIntent || cancelledThisTick.Contains(pawn.Id) ||
                    pawn.HasPendingCommand || pawn.Status == MovementStatus.Moving || pawn.Status == MovementStatus.Blocked) continue;
                if (pawn.LastWorkRevision == availabilityRevision && Tick < pawn.NextWorkSearchTick) continue;
                ResourceState source = null; IReadOnlyList<GridPosition> path = null;
                var reason = "NoDesignation";
                foreach (var item in Resources)
                {
                    if (!item.Exists || item.Amount <= 0 || item.StorageSlotId.HasValue || item.CreatedTick >= Tick) continue;
                    if (item.OwnerId.HasValue) { reason = "TargetReserved"; continue; }
                    var approach = FindPath(pawn.Position, item.Position);
                    if (approach == null) { reason = "Unreachable"; continue; }
                    if (ChooseStorage(item.Kind, item.Position, out _) == null) { reason = StorageFailure(item.Kind); continue; }
                    if (source == null || approach.Count < path.Count || approach.Count == path.Count && item.Id < source.Id)
                    { source = item; path = approach; }
                }
                if (source != null)
                {
                    EndLife(pawn);
                    if (source.Reserve(pawn.Id, pawn.ActionId, Math.Min(10, source.Amount)))
                    {
                        pawn.WorkTargetId = source.Id; pawn.WorkActivity = WorkActivity.GoingToPickup;
                        pawn.SetRoute(source.Position, path); pawn.WorkReason = string.Empty; availabilityRevision++; continue;
                    }
                }
                GatherState target = null; path = null;
                foreach (var node in gatherables)
                {
                    if (!node.Designated || node.Depleted) continue;
                    if (node.OwnerId.HasValue) { reason = "TargetReserved"; continue; }
                    var approach = FindPath(pawn.Position, node.Position);
                    if (approach == null) { reason = "Unreachable"; continue; }
                    if (target == null || approach.Count < path.Count || approach.Count == path.Count &&
                        (node.RequestOrder < target.RequestOrder || node.RequestOrder == target.RequestOrder && node.Id < target.Id))
                    { target = node; path = approach; }
                }
                if (target != null)
                {
                    EndLife(pawn); target.Claim.Acquire(pawn.Id, pawn.ActionId);
                    pawn.WorkActivity = WorkActivity.GoingToGather; pawn.WorkTargetId = target.Id;
                    pawn.SetRoute(target.Position, path); pawn.WorkReason = string.Empty; availabilityRevision++;
                }
                else
                {
                    pawn.WorkReason = reason; pawn.LastWorkRevision = availabilityRevision; pawn.NextWorkSearchTick = Tick + 10;
                }
            }
        }
        private string StorageFailure(ResourceKind kind)
        {
            var allowed = false; var free = false;
            foreach (var zone in storageZones) if (zone.Allows(kind))
            {
                allowed = true;
                foreach (var slot in zone.Slots)
                    if (slot.Amount < StorageSlotState.Capacity && (slot.Amount == 0 || slot.Stack.Kind == kind))
                    { if (slot.OwnerId.HasValue) return "DestinationReserved"; free = true; }
            }
            return !allowed ? "NoAllowedStorage" : free ? "Unreachable" : "StorageFull";
        }
        private void ProgressWork()
        {
            foreach (var pawn in Colonists)
            {
                if (!pawn.IsWorking || Tick < pawn.WorkReadyTick) continue;
                switch (pawn.WorkActivity)
                {
                    case WorkActivity.Gathering:
                        var node = Gather(pawn.WorkTargetId.Value);
                        node.Progress++;
                        if (node.Progress < node.Duration) break;
                        node.Depleted = true; node.Designated = false;
                        SpawnResource(node.ResourceKind, node.Yield, node.Position);
                        produced[node.ResourceKind] = ProducedAmount(node.ResourceKind) + node.Yield;
                        EndLife(pawn); cancelledThisTick.Add(pawn.Id);
                        break;
                    case WorkActivity.PickingUp:
                        var source = resourcesById[pawn.WorkTargetId.Value];
                        var destination = ChooseStorage(source.Kind, source.Position, out var route);
                        if (destination == null) { EndLife(pawn); pawn.WorkReason = StorageFailure(source.Kind); cancelledThisTick.Add(pawn.Id); break; }
                        var amount = Math.Min(Math.Min(10, source.Amount), StorageSlotState.Capacity - destination.Amount);
                        if (!destination.Claim.Acquire(pawn.Id, pawn.ActionId)) break;
                        source.TransferOut(amount); source.Release(pawn.Id, pawn.ActionId);
                        pawn.CargoKind = source.Kind; pawn.CargoAmount = amount;
                        pawn.WorkTargetId = destination.Id; pawn.WorkActivity = WorkActivity.GoingToStore;
                        pawn.SetRoute(destination.Position, route); availabilityRevision++;
                        break;
                    case WorkActivity.Storing:
                        var slot = Slot(pawn.WorkTargetId.Value);
                        if (slot.Stack == null || !slot.Stack.Exists || slot.Amount == 0 && slot.Stack.Kind != pawn.CargoKind)
                        {
                            if (slot.Stack != null) slot.Stack.DetachStorage();
                            slot.Stack = SpawnResource(pawn.CargoKind, 0, slot.Position);
                            slot.Stack.StorageSlotId = slot.Id; slot.Stack.AttachClaim(slot.Claim);
                        }
                        slot.Stack.TransferIn(pawn.CargoAmount); pawn.CargoAmount = 0;
                        EndLife(pawn); cancelledThisTick.Add(pawn.Id); availabilityRevision++;
                        break;
                }
            }
        }
        private void HandleWorkArrivals()
        {
            foreach (var pawn in Colonists)
            {
                if (pawn.Status != MovementStatus.Arrived) continue;
                switch (pawn.WorkActivity)
                {
                    case WorkActivity.GoingToGather: pawn.WorkActivity = WorkActivity.Gathering; break;
                    case WorkActivity.GoingToPickup: pawn.WorkActivity = WorkActivity.PickingUp; break;
                    case WorkActivity.GoingToStore: pawn.WorkActivity = WorkActivity.Storing; break;
                    default: continue;
                }
                pawn.WorkReadyTick = Tick + 1;
            }
        }
    }
}
