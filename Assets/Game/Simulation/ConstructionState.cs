namespace RimworldTweak.Simulation
{
    public enum BuildingKind { Wall, Door, Bed, Stove }
    public sealed class BlueprintState
    {
        internal BlueprintState(int id, BuildingKind kind, GridPosition position, long requestOrder)
        { Id = id; Kind = kind; Position = position; RequestOrder = requestOrder; }
        internal readonly InteractionClaim Claim = new InteractionClaim();
        public int Id { get; }
        public BuildingKind Kind { get; }
        public GridPosition Position { get; }
        public long RequestOrder { get; internal set; }
        public bool Exists { get; internal set; } = true;
        public int DeliveredWood { get; internal set; }
        public int Progress { get; internal set; }
        public WorkReasonCode Reason { get; internal set; }
        public int Cost => ConstructionRules.Cost(Kind);
        public int Duration => ConstructionRules.Duration(Kind);
        public int? OwnerId => Claim.OwnerId;
        public long OwnerActionId => Claim.ActionId;
    }
    public sealed class BuildingState
    {
        internal BuildingState(int id, BuildingKind kind, GridPosition position)
        { Id = id; Kind = kind; Position = position; }
        internal readonly InteractionClaim Claim = new InteractionClaim();
        public int Id { get; }
        public BuildingKind Kind { get; }
        public GridPosition Position { get; }
        public bool Exists { get; internal set; } = true;
        public bool DemolitionDesignated { get; internal set; }
        public int DemolitionProgress { get; internal set; }
        public long RequestOrder { get; internal set; }
        public WorkReasonCode Reason { get; internal set; }
        public int Cost => ConstructionRules.Cost(Kind);
        public int? OwnerId => Claim.OwnerId;
        public long OwnerActionId => Claim.ActionId;
    }
}
