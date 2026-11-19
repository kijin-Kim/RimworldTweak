namespace RimworldTweak.Simulation
{
    public enum WorkFeedbackKind { Cook, Construct, Demolish, FellTree, GatherPlant, Sow, Harvest }

    public readonly struct WorkFeedback
    {
        public int PawnId { get; }
        public long ActionId { get; }
        public WorkFeedbackKind Kind { get; }
        public int TargetId { get; }
        public GridPosition TargetCell { get; }
        public int Progress { get; }
        public int Duration { get; }

        internal WorkFeedback(ColonistState pawn, WorkFeedbackKind kind, int targetId, GridPosition targetCell, int progress, int duration)
        {
            PawnId = pawn.Id;
            ActionId = pawn.ActionId;
            Kind = kind;
            TargetId = targetId;
            TargetCell = targetCell;
            Progress = progress;
            Duration = duration;
        }
    }

    public sealed partial class WorldState
    {
        // Read the current owner's work without preparing candidates or updating diagnostic reasons.
        public bool TryGetWorkFeedback(int pawnId, out WorkFeedback feedback)
        {
            feedback = default;
            if (!colonistsById.TryGetValue(pawnId, out var pawn) || pawn.IsDrafted ||
                pawn.Status != MovementStatus.Arrived || !pawn.WorkTargetId.HasValue)
                return false;

            var id = pawn.WorkTargetId.Value;
            if (pawn.WorkActivity == WorkActivity.Gathering)
            {
                var node = Gather(id);
                if (node == null || node.Depleted || !node.Designated || blockedSet.Contains(node.Position) ||
                    !node.Claim.Matches(pawn.Id, pawn.ActionId)) return false;
                var kind = node.Kind == GatherKind.Tree ? WorkFeedbackKind.FellTree : WorkFeedbackKind.GatherPlant;
                feedback = new WorkFeedback(pawn, kind, id, node.Position, node.Progress, node.Duration);
                return true;
            }

            if (pawn.WorkActivity != WorkActivity.WorkingFeature || !ValidProductionWork(pawn)) return false;
            switch (pawn.FeatureStep)
            {
                case WorkStep.Cook:
                    var stove = Building(id);
                    var order = CookingOrder(id);
                    if (CookingOutputReason(CookingOutput(id)) != WorkReasonCode.None) return false;
                    feedback = new WorkFeedback(pawn, WorkFeedbackKind.Cook, id, stove.Position, order.Progress, CookingRules.Duration);
                    return true;
                case WorkStep.Construct:
                    var site = Blueprint(id);
                    if (!Clearance(site)) return false;
                    feedback = new WorkFeedback(pawn, WorkFeedbackKind.Construct, id, site.Position, site.Progress, site.Duration);
                    return true;
                case WorkStep.Demolish:
                    var building = Building(id);
                    feedback = new WorkFeedback(pawn, WorkFeedbackKind.Demolish, id, building.Position,
                        building.DemolitionProgress, ConstructionRules.DemolitionDuration);
                    return true;
                case WorkStep.Sow:
                case WorkStep.Harvest:
                    var crop = Crop(id);
                    feedback = pawn.FeatureStep == WorkStep.Sow
                        ? new WorkFeedback(pawn, WorkFeedbackKind.Sow, id, crop.Position, crop.SowProgress, FarmingRules.SowDuration)
                        : new WorkFeedback(pawn, WorkFeedbackKind.Harvest, id, crop.Position, crop.HarvestProgress, FarmingRules.HarvestDuration);
                    return true;
                default:
                    return false;
            }
        }
    }
}
