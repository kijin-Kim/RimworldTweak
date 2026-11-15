using System;
using System.Collections.Generic;

namespace RimworldTweak.Simulation
{
    public sealed partial class WorldState
    {
        private readonly object workContextToken = new object();
        private bool CanSelectWork(ColonistState pawn) => !pawn.IsDrafted && !pawn.IsLifeBusy && !pawn.IsWorking &&
            !pawn.HasSleepIntent && !cancelledThisTick.Contains(pawn.Id) && !pawn.HasPendingCommand &&
            pawn.Status != MovementStatus.Moving && pawn.Status != MovementStatus.Blocked;

        // S0 preparation may count path searches. Consuming the returned context is pure.
        internal WorkReadContext PrepareWorkContext(int pawnId)
        {
            var pawn = colonistsById[pawnId];
            var context = new WorkReadContext(workContextToken, Tick, availabilityRevision, MapRevision, pawn);
            var candidates = new List<WorkCandidate>();
            var reason = WorkReasonCode.NoDesignation;
            if (!CanSelectWork(pawn))
            { context.Seal(candidates, pawn.IsDrafted ? WorkReasonCode.Drafted : WorkReasonCode.NeedsPriority); return context; }
            foreach (var item in Resources)
            {
                if (!item.Exists || item.Amount <= 0 || item.StorageSlotId.HasValue || item.CreatedTick >= Tick) continue;
                if (item.OwnerId.HasValue) { reason = WorkReasonCode.TargetReserved; continue; }
                var approach = FindPath(pawn.Position, item.Position);
                if (approach == null) { reason = WorkReasonCode.Unreachable; continue; }
                if (ChooseStorage(item.Kind, item.Position, out _) == null)
                { reason = (WorkReasonCode)Enum.Parse(typeof(WorkReasonCode), StorageFailure(item.Kind)); continue; }
                // Legacy haul has no fixed destination until pickup: target/source both identify the stack.
                candidates.Add(new WorkCandidate(context, WorkCategory.Haul, WorkStep.HaulToStorage,
                    item.Id, item.Id, item.Position, 0, approach));
            }
            if (candidates.Count == 0) foreach (var node in gatherables)
                {
                    if (!node.Designated || node.Depleted) continue;
                    if (node.OwnerId.HasValue) { reason = WorkReasonCode.TargetReserved; continue; }
                    var approach = FindPath(pawn.Position, node.Position);
                    if (approach == null) { reason = WorkReasonCode.Unreachable; continue; }
                    candidates.Add(new WorkCandidate(context, WorkCategory.Gather, WorkStep.Gather,
                        node.Id, null, node.Position, node.RequestOrder, approach));
                }
            candidates.Sort((a, b) => {
                var result = a.PathLength.CompareTo(b.PathLength);
                if (result == 0) result = a.RequestOrder.CompareTo(b.RequestOrder);
                return result != 0 ? result : a.TargetId.CompareTo(b.TargetId);
            });
            context.Seal(candidates, candidates.Count > 0 ? WorkReasonCode.None : reason);
            return context;
        }

        internal bool TryStartFeatureWork(int pawnId, WorkCandidate candidate)
        {
            if (candidate == null || !colonistsById.TryGetValue(pawnId, out var pawn) || !CanSelectWork(pawn)) return false;
            var context = candidate.Context;
            if (!ReferenceEquals(context.WorldToken, workContextToken) || context.PawnId != pawnId ||
                context.Tick != Tick || context.Revision != availabilityRevision || context.MapRevision != MapRevision ||
                context.ActionId != pawn.ActionId || context.Position != pawn.Position) return false;
            // Only candidates issued by S0 are executable; feature rules cannot invent an authority token.
            var issued = false; foreach (var entry in context.Candidates) if (ReferenceEquals(entry, candidate)) issued = true;
            if (!issued) return false;
            if (candidate.Step == WorkStep.Gather && candidate.Category == WorkCategory.Gather)
            {
                var node = Gather(candidate.TargetId);
                if (node == null || !node.Designated || node.Depleted || node.OwnerId.HasValue) return false;
                EndLife(pawn);
                node.Claim.Acquire(pawn.Id, pawn.ActionId);
                pawn.WorkActivity = WorkActivity.GoingToGather;
            }
            else if (candidate.Step == WorkStep.HaulToStorage && candidate.Category == WorkCategory.Haul)
            {
                if (!resourcesById.TryGetValue(candidate.TargetId, out var source) || !source.Exists || source.Amount <= 0 ||
                    source.OwnerId.HasValue || source.StorageSlotId.HasValue || source.CreatedTick >= Tick) return false;
                EndLife(pawn);
                source.Reserve(pawn.Id, pawn.ActionId, Math.Min(10, source.Amount));
                pawn.WorkActivity = WorkActivity.GoingToPickup;
            }
            else return false;
            pawn.WorkTargetId = candidate.TargetId; pawn.SetRoute(candidate.WorkCell, candidate.Route);
            pawn.WorkReason = string.Empty; availabilityRevision++; return true;
        }

        internal void CancelFeatureWork(int pawnId, long actionId, WorkCancelReason reason)
        {
            if (colonistsById.TryGetValue(pawnId, out var pawn) && pawn.ActionId == actionId) EndLife(pawn);
        }
    }
}
