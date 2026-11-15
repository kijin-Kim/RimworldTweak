using System;
using System.Collections.Generic;
namespace RimworldTweak.Simulation
{
    public static class ConstructionRules
    {
        public const int DemolitionDuration = 30;
        public static int Cost(BuildingKind kind)
        { switch (kind) { case BuildingKind.Wall: return 2; case BuildingKind.Door: return 4;
            case BuildingKind.Bed: return 10; case BuildingKind.Stove: return 15;
            default: throw new ArgumentOutOfRangeException(nameof(kind)); } }
        public static int Duration(BuildingKind kind)
        { switch (kind) { case BuildingKind.Wall: return 40; case BuildingKind.Door: return 60;
            case BuildingKind.Bed: return 100; case BuildingKind.Stove: return 120;
            default: throw new ArgumentOutOfRangeException(nameof(kind)); } }
        public static bool BlocksMovement(BuildingKind kind) => kind == BuildingKind.Wall || kind == BuildingKind.Stove;
        public static GridPosition ServiceCell(GridPosition position) => new GridPosition(position.X, position.Y - 1);
        public static IReadOnlyList<WorkCandidate> GetCandidates(WorkReadContext context, int pawnId)
        {
            var result = new List<WorkCandidate>();
            if (context.PawnId == pawnId)
                foreach (var option in context.Options)
                    if (option.Candidate.Category == WorkCategory.Build && option.Reason == WorkReasonCode.None)
                        result.Add(option.Candidate);
            return result.AsReadOnly();
        }
        public static FeatureWorkResult EvaluateStep(WorkReadContext context, int pawnId)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var work = context.ActiveWork;
            if (work == null || work.PawnId != pawnId || context.PawnId != pawnId ||
                (work.Step != WorkStep.Construct && work.Step != WorkStep.Demolish))
                return new FeatureWorkResult(work, work?.Progress ?? 0, false, ResourceKind.Wood, 0, ResourceKind.Wood, 0, WorkReasonCode.InvalidTarget);
            if (work.Progress < 0 || work.Progress >= work.Duration || work.Duration <= 0 || work.RequiredInput <= 0 ||
                work.Step == WorkStep.Demolish && work.Duration != DemolitionDuration)
                return new FeatureWorkResult(work, work.Progress, false, ResourceKind.Wood, 0, ResourceKind.Wood, 0, WorkReasonCode.InvalidTarget);
            if (work.Reason != WorkReasonCode.None)
                return new FeatureWorkResult(work, work.Progress, false, ResourceKind.Wood, 0, ResourceKind.Wood, 0, work.Reason);
            var progress = Math.Min(work.Duration, work.Progress + 1);
            var completed = progress == work.Duration;
            return new FeatureWorkResult(work, progress, completed, ResourceKind.Wood,
                completed && work.Step == WorkStep.Construct ? work.RequiredInput : 0, ResourceKind.Wood,
                completed && work.Step == WorkStep.Demolish ? work.RequiredInput : 0, WorkReasonCode.None);
        }
    }
}
