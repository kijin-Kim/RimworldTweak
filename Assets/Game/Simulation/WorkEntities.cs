using System.Collections.Generic;

namespace RimworldTweak.Simulation
{
    // A stored stack and its slot share this exact authority, not mirrored owners.
    internal sealed class InteractionClaim
    {
        public int? OwnerId { get; private set; }
        public long ActionId { get; private set; }
        public bool Acquire(int owner, long action)
        {
            if (OwnerId.HasValue) return false;
            OwnerId = owner; ActionId = action; return true;
        }
        public bool Matches(int owner, long action) => OwnerId == owner && ActionId == action;
        public void Release(int owner, long action) { if (Matches(owner, action)) Clear(); }
        public void Clear() { OwnerId = null; ActionId = 0; }
    }

    public enum WorkActivity { Idle, GoingToGather, Gathering, GoingToPickup, PickingUp, GoingToStore, Storing, GoingToFeature, WorkingFeature, GoingToBuildPickup, PickingUpBuild, GoingToBuildDelivery, DeliveringBuild }
    public enum GatherKind { Tree, EdiblePlant }

    public sealed class GatherSeed
    {
        public GatherSeed(int id, GatherKind kind, GridPosition position) { Id = id; Kind = kind; Position = position; }
        public int Id { get; }
        public GatherKind Kind { get; }
        public GridPosition Position { get; }
    }

    public sealed partial class GatherState
    {
        internal readonly InteractionClaim Claim = new InteractionClaim();
        internal GatherState(GatherSeed seed) { Id = seed.Id; Kind = seed.Kind; Position = seed.Position; }
        public int Id { get; }
        public GatherKind Kind { get; }
        public GridPosition Position { get; }
        public bool Designated { get; internal set; }
        public bool Depleted { get; internal set; }
        public int Progress { get; internal set; }
        public int Duration => Kind == GatherKind.Tree ? 60 : 30;
        public int Yield => Kind == GatherKind.Tree ? 10 : 4;
        public ResourceKind ResourceKind => Kind == GatherKind.Tree ? ResourceKind.Wood : ResourceKind.Ingredients;
        public int? OwnerId => Claim.OwnerId;
        public long OwnerActionId => Claim.ActionId;
        public long RequestOrder { get; internal set; }
    }

    public sealed partial class StorageSlotState
    {
        internal readonly InteractionClaim Claim = new InteractionClaim();
        internal StorageSlotState(int id, int zoneId, GridPosition position) { Id = id; ZoneId = zoneId; Position = position; }
        public const int Capacity = 50;
        public int Id { get; }
        public int ZoneId { get; }
        public GridPosition Position { get; }
        public ResourceState Stack { get; internal set; }
        public int Amount => Stack != null && Stack.Exists ? Stack.Amount : 0;
        public int? OwnerId => Claim.OwnerId;
        public long OwnerActionId => Claim.ActionId;
    }

    public sealed partial class StorageZoneState
    {
        private readonly HashSet<ResourceKind> allowed = new HashSet<ResourceKind> { ResourceKind.Wood, ResourceKind.Ingredients, ResourceKind.Meal };
        internal StorageZoneState(int id, List<StorageSlotState> slots) { Id = id; Slots = slots.AsReadOnly(); }
        public int Id { get; }
        public bool Exists { get; internal set; } = true;
        public IReadOnlyList<StorageSlotState> Slots { get; }
        public bool Allows(ResourceKind kind) => Exists && allowed.Contains(kind);
        internal void SetFilter(ResourceKind kind, bool value) { if (value) allowed.Add(kind); else allowed.Remove(kind); }
    }
}
