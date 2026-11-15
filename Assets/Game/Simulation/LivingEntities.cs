using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RimworldTweak.Simulation
{
    public sealed class BedSeed
    {
        public BedSeed(int id, GridPosition position) { Id = id; Position = position; }
        public int Id { get; }
        public GridPosition Position { get; }
    }

    public sealed partial class ResourceState
    {
        private readonly Dictionary<int, int> reservations = new Dictionary<int, int>();
        internal ResourceState(ResourceSeed seed)
        {
            Id = seed.Id; Kind = seed.Kind; Position = seed.Position; Amount = seed.Amount;
            Reservations = new ReadOnlyDictionary<int, int>(reservations);
        }
        public int Id { get; }
        public ResourceKind Kind { get; }
        public GridPosition Position { get; }
        public int Amount { get; private set; }
        public int ReservedAmount { get { var sum = 0; foreach (var amount in reservations.Values) sum += amount; return sum; } }
        public int AvailableAmount => Amount - ReservedAmount;
        public int AccessibleAmount => Exists && !OwnerId.HasValue ? Amount : 0;
        internal InteractionClaim Claim { get; private set; } = new InteractionClaim();
        public int? StorageSlotId { get; internal set; }
        internal long CreatedTick { get; set; } = -1;
        public int? OwnerId => Claim.OwnerId;
        public long OwnerActionId => Claim.ActionId;
        internal void AttachClaim(InteractionClaim claim) { Claim = claim; }
        internal void DetachStorage() { StorageSlotId = null; Claim = new InteractionClaim(); reservations.Clear(); IsInUse = false; }
        // Transfers never count as production, consumption, or external mutation.
        internal void TransferOut(int amount) { Amount -= amount; }
        internal void TransferIn(int amount) { Amount += amount; }

        public bool IsInUse { get; private set; }
        public bool Exists { get; private set; } = true;
        public int ConsumedAmount { get; private set; }
        public int AddedAmount { get; private set; }
        public int RemovedAmount { get; private set; }
        public IReadOnlyDictionary<int, int> Reservations { get; }
        internal bool Reserve(int owner, long actionId, int amount)
        {
            if (!Exists || amount <= 0 || Amount < amount || OwnerId.HasValue) return false;
            if (!Claim.Acquire(owner, actionId)) return false;
            reservations.Add(owner, amount); return true;
        }
        internal void Release(int owner, long actionId)
        {
            if (OwnerId != owner || OwnerActionId != actionId) return;
            ClearOwner();
        }
        private void ClearOwner()
        {
            reservations.Clear(); Claim.Clear(); IsInUse = false;
        }
        internal bool HasReservation(int owner, long actionId, int amount) => Exists && OwnerId == owner && OwnerActionId == actionId && Amount >= amount && reservations.TryGetValue(owner, out var held) && held == amount;
        internal void Occupy(int owner, long actionId)
        {
            if (Exists && OwnerId == owner && OwnerActionId == actionId) IsInUse = true;
        }
        internal bool Consume(int owner, long actionId, int amount)
        {
            if (!HasReservation(owner, actionId, amount)) return false;
            ClearOwner(); Amount -= amount; ConsumedAmount += amount; return true;
        }
        internal void SetAmount(int amount, bool exists)
        {
            if (amount > Amount) AddedAmount += amount - Amount; else RemovedAmount += Amount - amount;
            Amount = amount; Exists = exists;
            if (!exists || ReservedAmount > Amount) ClearOwner();
        }
    }

    public sealed partial class BedState
    {
        internal BedState(BedSeed seed) { Id = seed.Id; Position = seed.Position; }
        public int Id { get; }
        public GridPosition Position { get; }
        public bool Exists { get; private set; } = true;
        public int? OwnerId { get; private set; }
        public bool IsOccupied { get; private set; }
        public long OwnerActionId { get; private set; }
        internal bool Reserve(int owner, long actionId)
        {
            if (!Exists || OwnerId.HasValue) return false;
            OwnerId = owner; OwnerActionId = actionId; return true;
        }
        internal void Occupy(int owner, long actionId) { if (Exists && OwnerId == owner && OwnerActionId == actionId) IsOccupied = true; }
        internal void Release(int owner, long actionId) { if (OwnerId == owner && OwnerActionId == actionId) { OwnerId = null; OwnerActionId = 0; IsOccupied = false; } }
        internal void SetExists(bool exists) { Exists = exists; if (!exists) { OwnerId = null; OwnerActionId = 0; IsOccupied = false; } }
    }
}
