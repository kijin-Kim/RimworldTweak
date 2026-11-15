using System;
using System.Collections.Generic;

namespace RimworldTweak.Simulation
{
    public sealed class WorkPolicyState
    {
        private readonly int[] priorities = { 2, 1, 2, 2, 2 };
        internal WorkPolicyState(int pawnId) { PawnId = pawnId; }
        public int PawnId { get; }
        public int GetPriority(WorkCategory category)
        {
            if (!Enum.IsDefined(typeof(WorkCategory), category)) throw new ArgumentOutOfRangeException(nameof(category));
            return priorities[(int)category];
        }
        internal void SetPriority(WorkCategory category, int priority)
        {
            if (!Enum.IsDefined(typeof(WorkCategory), category)) throw new ArgumentOutOfRangeException(nameof(category));
            if (priority < 0 || priority > 3) throw new ArgumentOutOfRangeException(nameof(priority));
            priorities[(int)category] = priority;
        }
        public WorkPolicySnapshot CreateSnapshot() => new WorkPolicySnapshot(PawnId, priorities);
    }

    public sealed class WorkPolicySnapshot
    {
        private readonly int[] priorities;
        internal WorkPolicySnapshot(int pawnId, int[] priorities)
        { PawnId = pawnId; this.priorities = (int[])priorities.Clone(); }
        public int PawnId { get; }
        public int GetPriority(WorkCategory category)
        {
            if (!Enum.IsDefined(typeof(WorkCategory), category)) throw new ArgumentOutOfRangeException(nameof(category));
            return priorities[(int)category];
        }
    }

    public static class WorkPolicy
    {
        public static int DefaultPriority(WorkCategory category)
        {
            if (!Enum.IsDefined(typeof(WorkCategory), category)) throw new ArgumentOutOfRangeException(nameof(category));
            return category == WorkCategory.Haul ? 1 : 2;
        }
        public static WorkCandidate Select(WorkReadContext context, int pawnId, IReadOnlyList<WorkCandidate> candidates)
        {
            if (context == null || candidates == null || context.PawnId != pawnId || context.Policy == null ||
                context.Policy.PawnId != pawnId || context.Reason == WorkReasonCode.Drafted || context.Reason == WorkReasonCode.NeedsPriority) return null;
            WorkCandidate best = null;
            foreach (var candidate in candidates)
            {
                if (candidate == null || !ReferenceEquals(candidate.Context, context) || candidate.Reason != WorkReasonCode.None ||
                    context.Policy.GetPriority(candidate.Category) == 0) continue;
                var allowed = true;
                foreach (var option in context.Options)
                    if (ReferenceEquals(option.Candidate, candidate) && option.Reason != WorkReasonCode.None) { allowed = false; break; }
                if (allowed && (best == null || Compare(context.Policy, candidate, best) < 0)) best = candidate;
            }
            return best;
        }
        private static int Compare(WorkPolicySnapshot policy, WorkCandidate a, WorkCandidate b)
        {
            var result = policy.GetPriority(a.Category).CompareTo(policy.GetPriority(b.Category));
            if (result == 0) result = a.PathLength.CompareTo(b.PathLength);
            if (result == 0) result = a.RequestOrder.CompareTo(b.RequestOrder);
            if (result == 0) result = a.TargetId.CompareTo(b.TargetId);
            if (result == 0) result = a.Step.CompareTo(b.Step);
            return result;
        }
    }
}
