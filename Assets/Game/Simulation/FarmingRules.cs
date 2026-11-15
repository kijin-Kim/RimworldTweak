using System;
using System.Collections.Generic;

namespace RimworldTweak.Simulation
{
    public static class FarmingRules
    {
        public const int SowDuration = 20;
        public const int GrowthDuration = 2400;
        public const int HarvestDuration = 30;
        public const int HarvestYield = 6;

        public static IReadOnlyList<WorkCandidate> GetCandidates(WorkReadContext context, int pawnId)
        {
            var result = new List<WorkCandidate>();
            if (context == null || context.PawnId != pawnId) return result.AsReadOnly();
            foreach (var option in context.Options)
                if (option.Reason == WorkReasonCode.None && option.Candidate.Category == WorkCategory.Farm)
                    result.Add(option.Candidate);
            return result.AsReadOnly();
        }

        public static FeatureWorkResult EvaluateStep(WorkReadContext context, int pawnId)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var source = context.ActiveWork;
            if (source == null || source.PawnId != pawnId || context.PawnId != pawnId ||
                (source.Step != WorkStep.Sow && source.Step != WorkStep.Harvest))
                return new FeatureWorkResult(source, source?.Progress ?? 0, false, ResourceKind.Ingredients, 0,
                    ResourceKind.Ingredients, 0, WorkReasonCode.InvalidTarget);
            var reason = source.Reason;
            if (source.Progress < 0 || source.Progress >= source.Duration || source.Duration !=
                (source.Step == WorkStep.Sow ? SowDuration : HarvestDuration)) reason = WorkReasonCode.InvalidTarget;
            var progress = reason == WorkReasonCode.None ? source.Progress + 1 : source.Progress;
            var completed = reason == WorkReasonCode.None && progress == source.Duration;
            return new FeatureWorkResult(source, progress, completed, ResourceKind.Ingredients, 0,
                ResourceKind.Ingredients, completed && source.Step == WorkStep.Harvest ? HarvestYield : 0, reason);
        }

        public static int NextGrowthTicks(int growthTicks, long plantedTick, long currentTick)
        {
            if (growthTicks < 0 || growthTicks > GrowthDuration) throw new ArgumentOutOfRangeException(nameof(growthTicks));
            return currentTick <= plantedTick ? growthTicks : Math.Min(GrowthDuration, growthTicks + 1);
        }
    }
}
