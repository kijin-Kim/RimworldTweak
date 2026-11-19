using System;
using System.Collections.Generic;

namespace RimworldTweak.Simulation
{
    public sealed partial class WorldState
    {
        private readonly List<WorkPolicyState> workPolicies = new List<WorkPolicyState>();
        public IReadOnlyList<WorkPolicyState> WorkPolicies => workPolicies.AsReadOnly();
        private WorkPolicyState PolicyFor(int pawnId) => workPolicies.Find(p => p.PawnId == pawnId);
        private WorkCategory CurrentWorkCategory(ColonistState pawn)
        {
            if(pawn.IsCookingDelivery)return WorkCategory.Cook;
            if (pawn.FeatureStep == WorkStep.DeliverCook || pawn.FeatureStep == WorkStep.Cook) return WorkCategory.Cook;
            if (pawn.FeatureStep == WorkStep.Sow || pawn.FeatureStep == WorkStep.Harvest) return WorkCategory.Farm;
            if (pawn.FeatureStep == WorkStep.Care) return WorkCategory.Care;
            if (pawn.FeatureStep.HasValue) return WorkCategory.Build;
            return pawn.WorkActivity == WorkActivity.GoingToGather || pawn.WorkActivity == WorkActivity.Gathering ? WorkCategory.Gather : WorkCategory.Haul;
        }
        public bool SetWorkPriority(int pawnId, WorkCategory category, int priority)
        {
            if (!colonistsById.TryGetValue(pawnId, out var pawn) || !Enum.IsDefined(typeof(WorkCategory), category) || priority < 0 || priority > 3)
                return Reject("InvalidWorkPriority");
            var policy = PolicyFor(pawnId);
            if (policy.GetPriority(category) != priority)
            {
                policy.SetPriority(category, priority);
                if(priority>0 && pawn.WorkReason==WorkReasonCode.Disabled.ToString()) pawn.WorkReason=string.Empty;
                if (priority == 0 && pawn.IsWorking && CurrentWorkCategory(pawn) == category)
                { EndLife(pawn); cancelledThisTick.Add(pawn.Id); pawn.WorkReason = WorkReasonCode.Disabled.ToString(); }
                availabilityRevision++;
            }
            LastCommandError = string.Empty; return true;
        }
        public WorkReasonCode GetWorkDiagnostic(WorkCategory category,int? targetId=null)
        {
            var enabled=false;foreach(var policy in workPolicies)if(policy.GetPriority(category)>0)enabled=true;
            if(!enabled)return WorkReasonCode.NoWorker;
            var reasons=new List<WorkReasonCode>();
            if(category==WorkCategory.Cook)
                foreach(var o in cookingOrders)if(o.Exists&&(!targetId.HasValue||o.StoveId==targetId))reasons.Add(CookingDiagnostic(o));
            if(category==WorkCategory.Build)
            {
                foreach(var b in blueprints)if(b.Exists&&(!targetId.HasValue||b.Id==targetId))
                    reasons.Add(b.OwnerId.HasValue?WorkReasonCode.TargetReserved:!AnyWorkerReaches(category,b.Position,true)?WorkReasonCode.Unreachable:
                        b.DeliveredWood>=b.Cost?(Clearance(b)?WorkReasonCode.None:WorkReasonCode.AwaitingClearance):MaterialDiagnostic(category,ResourceKind.Wood,b.Position,true));
                foreach(var b in buildings)if(b.Exists&&b.DemolitionDesignated&&(!targetId.HasValue||b.Id==targetId))
                    reasons.Add(b.OwnerId.HasValue?WorkReasonCode.TargetReserved:AnyWorkerReaches(category,b.Position,true)?WorkReasonCode.None:WorkReasonCode.Unreachable);
            }
            if(category==WorkCategory.Farm)
                foreach(var c in crops)if(c.Exists&&(!targetId.HasValue||c.Id==targetId)&&c.Stage!=CropStage.Growing)
                    reasons.Add(CropDiagnostic(c,GrowingZone(c.ZoneId)));
            if(category==WorkCategory.Gather)
                foreach(var n in gatherables)if(n.Designated&&!n.Depleted&&(!targetId.HasValue||n.Id==targetId))
                    reasons.Add(n.OwnerId.HasValue?WorkReasonCode.TargetReserved:AnyWorkerReaches(category,n.Position,false)?WorkReasonCode.None:WorkReasonCode.Unreachable);
            if(category==WorkCategory.Haul)
                foreach(var resource in Resources)if(resource.Exists&&resource.Amount>0&&!resource.StorageSlotId.HasValue&&(!targetId.HasValue||resource.Id==targetId))
                {
                    if(resource.OwnerId.HasValue){reasons.Add(WorkReasonCode.TargetReserved);continue;}
                    if(!AnyWorkerReaches(category,resource.Position,false)){reasons.Add(WorkReasonCode.Unreachable);continue;}
                    var reason=(WorkReasonCode)Enum.Parse(typeof(WorkReasonCode),StorageFailure(resource.Kind));
                    foreach(var zone in storageZones)if(zone.Allows(resource.Kind))foreach(var slot in zone.Slots)
                        if(!slot.OwnerId.HasValue&&slot.Amount<50&&(slot.Amount==0||slot.Stack.Kind==resource.Kind)&&HasPath(resource.Position,slot.Position))reason=WorkReasonCode.None;
                    reasons.Add(reason);
                }
            return WorkDiagnostics.ForCategory(category,workPolicies,reasons);
        }
        private WorkReasonCode CropDiagnostic(CropState crop, GrowingZoneState zone)
        {
            if (crop.Stage == CropStage.Empty && !zone.SowingEnabled) return WorkReasonCode.Disabled;
            if (crop.OwnerId.HasValue) return WorkReasonCode.TargetReserved;
            return AnyWorkerReaches(WorkCategory.Farm,crop.Position,false) ? WorkReasonCode.None : WorkReasonCode.Unreachable;
        }

        // All cells in a zone share the worker policy. Inspect each cell once for the alert panel.
        public int CountFarmAlerts(GrowingZoneState zone, out CropState first, out WorkReasonCode reason)
        {
            first = null;
            var staffed = workPolicies.Exists(p => p.GetPriority(WorkCategory.Farm) > 0);
            reason = staffed ? WorkReasonCode.Unreachable : WorkReasonCode.NoWorker;
            var count = 0;
            foreach (var crop in zone.Cells)
            {
                if (!crop.Exists || crop.Stage == CropStage.Growing) continue;
                if (staffed && CropDiagnostic(crop,zone) != reason) continue;
                if (first == null) first = crop;
                count++;
            }
            return count;
        }

        private bool ReadReach(GridPosition start,GridPosition target,bool adjacent)
        {
            if(!adjacent)return HasPath(start,target);
            foreach(var d in Directions)if(HasPath(start,new GridPosition(target.X+d.X,target.Y+d.Y)))return true;
            return false;
        }
        private bool AnyWorkerReaches(WorkCategory category,GridPosition target,bool adjacent)
        {foreach(var pawn in Colonists)if(!pawn.IsDowned&&PolicyFor(pawn.Id).GetPriority(category)>0&&ReadReach(pawn.Position,target,adjacent))return true;return false;}
        private WorkReasonCode MaterialDiagnostic(WorkCategory category,ResourceKind kind,GridPosition destination,bool adjacent)
        {
            var found=false;var reserved=false;
            foreach(var resource in Resources)if(resource.Exists&&resource.Kind==kind&&resource.Amount>0)
            {
                found=true;if(resource.OwnerId.HasValue){reserved=true;continue;}
                if(AnyWorkerReaches(category,resource.Position,false)&&ReadReach(resource.Position,destination,adjacent))return WorkReasonCode.None;
            }
            return !found?WorkReasonCode.MissingMaterials:reserved?WorkReasonCode.TargetReserved:WorkReasonCode.Unreachable;
        }
        private WorkReasonCode CookingDiagnostic(CookingOrderState o)
        {
            var b=Building(o.StoveId);if(b==null||b.DemolitionDesignated)return WorkReasonCode.InvalidTarget;
            if(!o.Enabled)return WorkReasonCode.Disabled;
            if(!NeedsCooking(o, MealTotal))return WorkReasonCode.TargetReached;
            var output=CookingOutput(o.StoveId);
            if(output==null||output.Amount>=50)return WorkReasonCode.OutputFull;
            if(o.OwnerId.HasValue||output.OwnerId.HasValue)return WorkReasonCode.TargetReserved;
            if(!AnyWorkerReaches(WorkCategory.Cook,o.ServiceCell,false))return WorkReasonCode.Unreachable;
            return o.InputIngredients>=2?WorkReasonCode.None:MaterialDiagnostic(WorkCategory.Cook,ResourceKind.Ingredients,o.ServiceCell,false);
        }
        private readonly object workContextToken = new object();
        private bool CanSelectWork(ColonistState pawn) => !pawn.IsDowned && (!pawn.PatientClaim.OwnerId.HasValue) && !pawn.IsDrafted && !pawn.IsLifeBusy && !pawn.IsWorking &&
            !pawn.HasSleepIntent && !cancelledThisTick.Contains(pawn.Id) && !pawn.HasPendingCommand &&
            pawn.Status != MovementStatus.Moving && pawn.Status != MovementStatus.Blocked;

        // S0 preparation may count path searches. Consuming the returned context is pure.
        internal WorkReadContext PrepareWorkContext(int pawnId)
        {
            var pawn = colonistsById[pawnId];
            var context = new WorkReadContext(workContextToken, Tick, availabilityRevision, MapRevision, pawn);
            context.SetPolicy(PolicyFor(pawnId).CreateSnapshot());
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
            foreach (var node in gatherables)
                {
                    if (!node.Designated || node.Depleted) continue;
                    if (node.OwnerId.HasValue) { reason = WorkReasonCode.TargetReserved; continue; }
                    var approach = FindPath(pawn.Position, node.Position);
                    if (approach == null) { reason = WorkReasonCode.Unreachable; continue; }
                    candidates.Add(new WorkCandidate(context, WorkCategory.Gather, WorkStep.Gather,
                        node.Id, null, node.Position, node.RequestOrder, approach));
                }
            PrepareProductionOptions(context, pawn);
            candidates.AddRange(ConstructionRules.GetCandidates(context, pawn.Id));
            candidates.AddRange(FarmingRules.GetCandidates(context, pawn.Id));
            candidates.AddRange(CookingRules.GetCandidates(context, pawn.Id));
            CollectRecoveryCandidates(context, pawn, candidates);
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
            if (!issued || PolicyFor(pawnId).GetPriority(candidate.Category) == 0) return false;
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
            else return IsRecovery(candidate.Step) ? StartRecovery(pawn, candidate) : StartProductionWork(pawn, candidate);
            pawn.WorkTargetId = candidate.TargetId; pawn.SetRoute(candidate.WorkCell, candidate.Route);
            pawn.WorkReason = string.Empty; availabilityRevision++; return true;
        }

        internal void CancelFeatureWork(int pawnId, long actionId, WorkCancelReason reason)
        {
            if (colonistsById.TryGetValue(pawnId, out var pawn) && pawn.ActionId == actionId) EndLife(pawn);
        }
    }
}
