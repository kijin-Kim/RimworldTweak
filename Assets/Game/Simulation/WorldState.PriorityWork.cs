using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace RimworldTweak.Simulation
{
    [DataContract]
    public sealed class PriorityWorkSaveData
    {
        [DataMember(IsRequired = true)] public int PawnId;
        [DataMember(IsRequired = true)] public int TargetId;
    }

    public sealed partial class WorldState
    {
        private readonly Dictionary<int, int> priorityOrders = new Dictionary<int, int>();

        public int? PriorityWorkTarget(int pawnId) =>
            priorityOrders.TryGetValue(pawnId, out var target) ? target : (int?)null;

        private bool PriorityTargetHeldByOther(int pawnId, int targetId) =>
            priorityOrders.Any(order => order.Key != pawnId && order.Value == targetId);

        private static int? PersonalWorkSite(ColonistState pawn)
        {
            if (pawn.WorkActivity == WorkActivity.GoingToGather || pawn.WorkActivity == WorkActivity.Gathering)
                return pawn.WorkTargetId;
            switch (pawn.FeatureStep)
            {
                case WorkStep.DeliverBuild:
                case WorkStep.DeliverRepair: return pawn.PendingDeliveryTargetId;
                case WorkStep.Construct:
                case WorkStep.Repair: return pawn.WorkTargetId;
                default: return null;
            }
        }

        public bool PrioritizeWork(int pawnId, int targetId)
        {
            if (!colonistsById.TryGetValue(pawnId, out var pawn)) return Reject("InvalidColonist");
            var node = Gather(targetId);
            var blueprint = Blueprint(targetId);
            var site = Building(targetId);
            var gather = node != null && node.Kind == GatherKind.Tree && node.Designated && !node.Depleted;
            if (!gather && blueprint == null && !CanRepair(site)) return Reject("NoDesignation");
            if (pawn.IsDrafted) return Reject("Drafted");
            if (pawn.IsDowned || pawn.PatientClaim.OwnerId.HasValue || pawn.IsLifeBusy ||
                NeedsEnabled && (pawn.HasSleepIntent || pawn.Nutrition <= 3000 || pawn.Rest <= 2000))
                return Reject("NeedsPriority");
            var category = gather ? WorkCategory.Gather : WorkCategory.Build;
            if (PolicyFor(pawnId).GetPriority(category) == 0) return Reject("Disabled");

            // Material delivery reserves its source before it reserves the building site.
            if (Colonists.Any(other => other.Id != pawnId && PersonalWorkSite(other) == targetId))
                return Reject("TargetReserved");
            if (PersonalWorkSite(pawn) == targetId)
            {
                priorityOrders[pawnId] = targetId;
                LastCommandError = string.Empty;
                return true;
            }
            var owner = gather ? node.OwnerId : blueprint != null ? blueprint.OwnerId : site.OwnerId;
            if (owner.HasValue) return Reject("TargetReserved");

            var destination = gather ? node.Position : blueprint != null ? blueprint.Position : site.Position;
            var route = gather ? ReadPath(pawn.Position, destination) : ReadApproachPath(pawn.Position, destination);
            if (route == null) return Reject("Unreachable");
            var cell = route.Count == 0 ? pawn.Position : route[route.Count - 1];
            WorkStep step;
            ResourceState source = null;
            var useCargo = false;
            if (gather) step = WorkStep.Gather;
            else if (blueprint != null && blueprint.DeliveredWood >= blueprint.Cost)
            {
                if (!Clearance(blueprint)) return Reject("AwaitingClearance");
                step = WorkStep.Construct;
            }
            else if (site != null && site.RepairWood >= 2) step = WorkStep.Repair;
            else
            {
                step = blueprint != null ? WorkStep.DeliverBuild : WorkStep.DeliverRepair;
                useCargo = pawn.CargoKind == ResourceKind.Wood && pawn.CargoAmount > 0;
                if (useCargo)
                {
                    route = Array.Empty<GridPosition>();
                    cell = pawn.Position;
                }
                else
                {
                    var reason = FindPriorityWood(pawn, destination, out source, out route);
                    if (reason != WorkReasonCode.None) return Reject(reason.ToString());
                    cell = source.Position;
                }
            }

            // All failure checks precede cancellation. Transfer current cargo via ClearWork's normal drop.
            var firstNewResource = mutableResources.Count;
            StopDirectOrders(pawnId);
            if (useCargo || source?.OwnerId == pawnId) EndLife(pawn);
            if (useCargo) source = mutableResources.Skip(firstNewResource).First(r => r.Kind == ResourceKind.Wood && r.Position == pawn.Position);
            var context = new WorkReadContext(workContextToken, Tick, availabilityRevision, MapRevision, pawn);
            var candidate = new WorkCandidate(context, category, step, targetId, source?.Id, cell, 0, route);
            if (!StartValidatedWork(pawn, candidate)) return Reject("InvalidTarget");
            priorityOrders[pawnId] = targetId;
            LastCommandError = string.Empty;
            return true;
        }

        private WorkReasonCode FindPriorityWood(ColonistState pawn, GridPosition destination,
            out ResourceState best, out IReadOnlyList<GridPosition> route)
        {
            best = null;
            route = null;
            var available = false;
            var reserved = false;
            foreach (var item in Resources)
            {
                if (!item.Exists || item.Kind != ResourceKind.Wood || item.Amount <= 0) continue;
                if (item.OwnerId.HasValue && item.OwnerId != pawn.Id) { reserved = true; continue; }
                available = true;
                var path = ReadPath(pawn.Position, item.Position);
                if (path == null || ReadApproachPath(item.Position, destination) == null) continue;
                if (best != null && (path.Count > route.Count || path.Count == route.Count && item.Id > best.Id)) continue;
                best = item;
                route = path;
            }
            if (best != null) return WorkReasonCode.None;
            if (available) return WorkReasonCode.Unreachable;
            return reserved ? WorkReasonCode.TargetReserved : WorkReasonCode.MissingMaterials;
        }

        public bool CancelPriorityWork(int pawnId)
        {
            if (!priorityOrders.ContainsKey(pawnId)) return Reject("InvalidTarget");
            ReleasePriorityOrder(pawnId);
            LastCommandError = string.Empty;
            return true;
        }

        private void ReleasePriorityOrder(int pawnId)
        {
            if (!priorityOrders.Remove(pawnId)) return;
            EndLife(colonistsById[pawnId]);
            cancelledThisTick.Add(pawnId);
        }

        // A delivery ends one underlying job. Preserve the explicit site through the next delivery or work stage.
        private void FinishPriorityStage(ColonistState pawn)
        {
            var target = PriorityWorkTarget(pawn.Id);
            FinishProduction(pawn);
            if (!target.HasValue || Blueprint(target.Value) == null && !CanRepair(Building(target.Value))) return;
            var commandError = LastCommandError;
            if (!PrioritizeWork(pawn.Id, target.Value)) pawn.WorkReason = LastCommandError;
            LastCommandError = commandError;
        }

        private List<PriorityWorkSaveData> CapturePriorityWork() => priorityOrders.Count == 0 ? null :
            priorityOrders.OrderBy(pair => pair.Key).Select(pair => new PriorityWorkSaveData {
                PawnId = pair.Key, TargetId = pair.Value
            }).ToList();

        private void RestorePriorityWork(List<PriorityWorkSaveData> orders)
        {
            if (orders == null) return;
            var targets = new HashSet<int>();
            foreach (var order in orders)
            {
                RequireSave(order != null && colonistsById.ContainsKey(order.PawnId));
                var pawn = colonistsById[order.PawnId];
                var node = Gather(order.TargetId);
                var validSite = node != null && node.Kind == GatherKind.Tree && node.Designated && !node.Depleted ||
                    Blueprint(order.TargetId) != null || CanRepair(Building(order.TargetId));
                RequireSave(validSite && !pawn.IsDrafted && !pawn.IsDowned && pawn.IsWorking &&
                    PersonalWorkSite(pawn) == order.TargetId && !priorityOrders.ContainsKey(pawn.Id) && targets.Add(order.TargetId) &&
                    !Colonists.Any(other => other.Id != pawn.Id && PersonalWorkSite(other) == order.TargetId) &&
                    PolicyFor(pawn.Id).GetPriority(node != null ? WorkCategory.Gather : WorkCategory.Build) > 0);
                priorityOrders.Add(pawn.Id, order.TargetId);
            }
        }
    }
}
