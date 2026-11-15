namespace RimworldTweak.Simulation
{
    public sealed partial class CookingOrderState
    {
        internal CookingOrderState(int id, int stoveId, GridPosition serviceCell, InteractionClaim claim)
        { Id=id; StoveId=stoveId; ServiceCell=serviceCell; Claim=claim; }
        internal readonly InteractionClaim Claim;
        public int Id { get; }
        public int StoveId { get; }
        public GridPosition ServiceCell { get; }
        public bool Exists { get; internal set; } = true;
        public bool Enabled { get; internal set; }
        public int TargetMeals { get; internal set; } = 12;
        public int InputIngredients { get; internal set; }
        public int Progress { get; internal set; }
        public bool ActiveCycle { get; internal set; }
        public long RequestOrder { get; internal set; }
        public WorkReasonCode Reason { get; internal set; }
        public int CompletedMeals { get; internal set; }
        public int? OwnerId => Claim.OwnerId;
        public long OwnerActionId => Claim.ActionId;
    }
    public sealed partial class CookingOutputState
    {
        internal CookingOutputState(int id, int stoveId, GridPosition position)
        { Id=id; StoveId=stoveId; Position=position; }
        internal readonly InteractionClaim Claim = new InteractionClaim();
        public const int Capacity = 50;
        public int Id { get; }
        public int StoveId { get; }
        public GridPosition Position { get; }
        public bool Exists { get; internal set; } = true;
        public ResourceState Stack { get; internal set; }
        public int Amount => Stack != null && Stack.Exists ? Stack.Amount : 0;
        public int? OwnerId => Claim.OwnerId;
        public long OwnerActionId => Claim.ActionId;
    }
}
