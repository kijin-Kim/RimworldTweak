using System;
using System.Collections.Generic;
using System.Linq;

namespace RimworldTweak.Simulation
{
    public sealed partial class WorldState
    {
        private const int RecoveryDuration = 40;
        public int MedicineConsumed { get; private set; }
        public int RepairWoodConsumed { get; private set; }

        private static bool IsRecovery(WorkStep? step) =>
            step == WorkStep.Care || step == WorkStep.DeliverRepair || step == WorkStep.Repair;

        private bool NeedsUrgentCareBreak(ColonistState patient) =>
            NeedsEnabled && !patient.IsDowned &&
            (patient.IsLifeBusy || patient.HasSleepIntent || patient.Rest <= 2000 || patient.Nutrition <= 3000);

        private bool BusyForCare(ColonistState patient) =>
            RescuerFor(patient.Id).HasValue || HasRescueCommand(patient.Id) || HasEquipOrder(patient.Id) ||
            PriorityWorkTarget(patient.Id).HasValue;

        private bool CanTreat(ColonistState patient) =>
            !BusyForCare(patient) && !patient.IsDrafted && patient.Health < 100 && !NeedsUrgentCareBreak(patient);

        private bool CanRepair(BuildingState site) =>
            site != null && site.RepairDesignated && !site.DemolitionDesignated && site.Durability < 100;

        public WorkReasonCode GetCareDiagnostic(int patientId)
        {
            if (!colonistsById.TryGetValue(patientId, out var patient) || patient.Health == 100)
                return WorkReasonCode.NoDesignation;
            if (patient.IsDrafted) return WorkReasonCode.Drafted;
            if (patient.TreatedBy.HasValue || BusyForCare(patient)) return WorkReasonCode.TargetReserved;
            if (NeedsUrgentCareBreak(patient)) return WorkReasonCode.NeedsPriority;
            var workers = Colonists.Where(p => !p.IsDowned && !p.IsDrafted && PolicyFor(p.Id).GetPriority(WorkCategory.Care) > 0);
            if (!workers.Any()) return WorkReasonCode.NoWorker;
            var stock = Resources.Where(r => r.Exists && r.Kind == ResourceKind.Medicine && r.Amount > 0).ToList();
            if (stock.Count == 0) return WorkReasonCode.MissingMaterials;
            if (stock.All(r => r.OwnerId.HasValue)) return WorkReasonCode.TargetReserved;
            foreach (var worker in workers)
                foreach (var source in stock)
                    if (!source.OwnerId.HasValue && ReadPath(worker.Position, source.Position) != null &&
                        (worker == patient || ReadApproachPath(source.Position, patient.Position) != null))
                        return WorkReasonCode.None;
            return WorkReasonCode.Unreachable;
        }

        public WorkReasonCode GetRepairDiagnostic(int facilityId)
        {
            var site = Building(facilityId);
            if (!CanRepair(site)) return WorkReasonCode.NoDesignation;
            if (site.OwnerId.HasValue)
                return colonistsById[site.OwnerId.Value].WorkReason == WorkReasonCode.AwaitingClearance.ToString() ?
                    WorkReasonCode.AwaitingClearance : WorkReasonCode.TargetReserved;
            if (!Colonists.Any(p => !p.IsDowned && !p.IsDrafted && PolicyFor(p.Id).GetPriority(WorkCategory.Build) > 0))
                return WorkReasonCode.NoWorker;
            if (site.RepairWood < 2) return MaterialDiagnostic(WorkCategory.Build, ResourceKind.Wood, site.Position, true);
            return AnyWorkerReaches(WorkCategory.Build, site.Position, true) ? WorkReasonCode.None : WorkReasonCode.Unreachable;
        }

        public bool SetRepair(int id, bool enabled)
        {
            var site = Building(id);
            if (site == null || enabled && (site.DemolitionDesignated || site.Durability == 100)) return Reject("InvalidRepair");
            if (site.RepairDesignated != enabled)
            {
                if (!enabled)
                {
                    foreach (var pawn in Colonists)
                        if ((pawn.FeatureStep == WorkStep.Repair && pawn.WorkTargetId == id) ||
                            (pawn.FeatureStep == WorkStep.DeliverRepair && pawn.PendingDeliveryTargetId == id))
                            FinishProduction(pawn);
                    if (site.RepairWood > 0) SpawnResource(ResourceKind.Wood, site.RepairWood, site.Position);
                    site.RepairWood = 0;
                    site.RepairProgress = 0;
                }
                site.RepairDesignated = enabled;
                availabilityRevision++;
            }
            LastCommandError = string.Empty;
            return true;
        }

        private IReadOnlyList<GridPosition> RecoveryApproach(GridPosition from,
            GridPosition target, bool self, out GridPosition cell)
        {
            if (!self) return ApproachSite(from, target, out cell);
            cell = from;
            return ReadPath(from, from);
        }

        private void CollectRecoveryCandidates(WorkReadContext context, ColonistState pawn, List<WorkCandidate> candidates)
        {
            if (PolicyFor(pawn.Id).GetPriority(WorkCategory.Care) > 0)
                foreach (var patient in Colonists)
                {
                    if (!CanTreat(patient) || patient.PatientClaim.OwnerId.HasValue ||
                        (patient != pawn && patient.FeatureStep == WorkStep.Care)) continue;
                    AddRecoveryDelivery(context, pawn, candidates, WorkCategory.Care, WorkStep.Care,
                        patient.Id, patient.Position, ResourceKind.Medicine, patient == pawn, patient.Health);
                }

            if (PolicyFor(pawn.Id).GetPriority(WorkCategory.Build) == 0) return;
            foreach (var site in buildings)
            {
                if (!CanRepair(site) || !site.Exists || site.Claim.OwnerId.HasValue) continue;
                if (site.RepairWood < 2)
                    AddRecoveryDelivery(context, pawn, candidates, WorkCategory.Build, WorkStep.DeliverRepair,
                        site.Id, site.Position, ResourceKind.Wood, false, site.RequestOrder);
                else
                {
                    var path = ApproachSite(pawn.Position, site.Position, out var cell);
                    if (path != null)
                        candidates.Add(new WorkCandidate(context, WorkCategory.Build, WorkStep.Repair, site.Id,
                            null, cell, site.RequestOrder, path));
                }
            }
        }

        private void AddRecoveryDelivery(WorkReadContext context, ColonistState pawn, List<WorkCandidate> candidates,
            WorkCategory category, WorkStep step, int targetId, GridPosition target, ResourceKind kind, bool self, long order)
        {
            foreach (var resource in Resources)
            {
                if (!resource.Exists || resource.Amount <= 0 || resource.Kind != kind || resource.OwnerId.HasValue) continue;
                var pickup = FindPath(pawn.Position, resource.Position);
                if (pickup == null || RecoveryApproach(resource.Position, target, self, out _) == null) continue;
                candidates.Add(new WorkCandidate(context, category, step, targetId, resource.Id, resource.Position, order, pickup));
            }
        }

        private bool StartRecovery(ColonistState pawn, WorkCandidate candidate)
        {
            var target = candidate.TargetId;
            if (candidate.Step == WorkStep.Care)
            {
                var patient = colonistsById[target];
                if (!CanTreat(patient) || patient.PatientClaim.OwnerId.HasValue ||
                    !ReserveRecoveryPickup(pawn, candidate, ResourceKind.Medicine, 1)) return false;
                if (patient != pawn && !RescuedBedFor(patient.Id).HasValue) EndLife(patient);
                patient.PatientClaim.Acquire(pawn.Id, pawn.ActionId);
            }
            else
            {
                var site = Building(target);
                if (!CanRepair(site) || site.Claim.OwnerId.HasValue) return false;
                if (candidate.Step == WorkStep.DeliverRepair)
                {
                    if (site.RepairWood >= 2 || !ReserveRecoveryPickup(pawn, candidate, ResourceKind.Wood, 2 - site.RepairWood)) return false;
                }
                else
                {
                    if (site.RepairWood != 2) return false;
                    EndLife(pawn);
                    site.Claim.Acquire(pawn.Id, pawn.ActionId);
                    pawn.WorkTargetId = target;
                    pawn.WorkActivity = WorkActivity.GoingToFeature;
                }
            }
            pawn.FeatureStep = candidate.Step;
            pawn.SetRoute(candidate.WorkCell, candidate.Route);
            availabilityRevision++;
            return true;
        }

        private bool ReserveRecoveryPickup(ColonistState pawn, WorkCandidate candidate, ResourceKind kind, int amount)
        {
            if (!candidate.SourceId.HasValue || !resourcesById.TryGetValue(candidate.SourceId.Value, out var source) ||
                !source.Exists || source.Kind != kind || source.Amount == 0 || source.OwnerId.HasValue) return false;
            EndLife(pawn);
            source.Reserve(pawn.Id, pawn.ActionId, Math.Min(amount, source.Amount));
            pawn.WorkTargetId = source.Id;
            pawn.PendingDeliveryTargetId = candidate.TargetId;
            pawn.WorkActivity = WorkActivity.GoingToBuildPickup;
            return true;
        }

        private void CancelPatientTreatment(ColonistState patient)
        {
            if (!patient.PatientClaim.OwnerId.HasValue) return;
            var worker = colonistsById[patient.PatientClaim.OwnerId.Value];
            if (patient.PatientClaim.Matches(worker.Id, worker.ActionId)) FinishProduction(worker);
        }

        private bool ValidRecoveryWork(ColonistState pawn)
        {
            var picking = pawn.WorkActivity == WorkActivity.GoingToBuildPickup || pawn.WorkActivity == WorkActivity.PickingUpBuild;
            if (pawn.FeatureStep == WorkStep.Care)
            {
                if (!colonistsById.TryGetValue(pawn.PendingDeliveryTargetId ?? -1, out var patient) ||
                    !CanTreat(patient) || !patient.PatientClaim.Matches(pawn.Id, pawn.ActionId)) return false;
                if (!picking) return pawn.WorkTargetId == patient.Id && pawn.CargoAmount == 1 &&
                    pawn.CargoKind == ResourceKind.Medicine;
            }
            else
            {
                var site = Building(pawn.PendingDeliveryTargetId ?? pawn.WorkTargetId ?? -1);
                if (!CanRepair(site)) return false;
                if (!picking) return site.Claim.Matches(pawn.Id, pawn.ActionId) &&
                    (pawn.FeatureStep == WorkStep.Repair ? site.RepairWood == 2 :
                        pawn.CargoAmount > 0 && pawn.CargoKind == ResourceKind.Wood && pawn.CargoAmount + site.RepairWood <= 2);
            }
            var kind = pawn.FeatureStep == WorkStep.Care ? ResourceKind.Medicine : ResourceKind.Wood;
            return resourcesById.TryGetValue(pawn.WorkTargetId ?? -1, out var source) && source.Exists &&
                source.Kind == kind && source.Amount > 0 && source.Claim.Matches(pawn.Id, pawn.ActionId) &&
                !blockedSet.Contains(source.Position);
        }

        private void ProgressRecovery()
        {
            foreach (var pawn in Colonists)
            {
                if (!IsRecovery(pawn.FeatureStep) || pawn.WorkReadyTick > Tick) continue;
                if (!ValidRecoveryWork(pawn) || pawn.Status == MovementStatus.Blocked)
                {
                    FinishProduction(pawn);
                    continue;
                }
                if (pawn.WorkActivity == WorkActivity.PickingUpBuild) PickUpRecoveryMaterial(pawn);
                else if (pawn.WorkActivity == WorkActivity.DeliveringBuild) DeliverRepairMaterial(pawn);
                else if (pawn.WorkActivity == WorkActivity.WorkingFeature)
                {
                    if (pawn.FeatureStep == WorkStep.Care) ProgressCare(pawn);
                    else ProgressRepair(pawn);
                }
            }
        }

        private void PickUpRecoveryMaterial(ColonistState pawn)
        {
            var source = resourcesById[pawn.WorkTargetId.Value];
            var id = pawn.PendingDeliveryTargetId.Value;
            var patient = pawn.FeatureStep == WorkStep.Care ? colonistsById[id] : null;
            var site = patient == null ? Building(id) : null;
            var route = RecoveryApproach(pawn.Position, patient?.Position ?? site.Position, patient == pawn, out var cell);
            if (route == null || site != null && !site.Claim.Acquire(pawn.Id, pawn.ActionId))
            {
                FinishProduction(pawn);
                return;
            }
            var amount = Math.Min(source.Reservations[pawn.Id], Math.Min(source.Amount, patient == null ? 2 - site.RepairWood : 1));
            if (amount <= 0) { FinishProduction(pawn); return; }
            source.TransferOut(amount);
            source.Release(pawn.Id, pawn.ActionId);
            pawn.CargoAmount = amount;
            pawn.CargoKind = source.Kind;
            pawn.WorkTargetId = id;
            pawn.WorkActivity = patient != null ? WorkActivity.GoingToFeature : WorkActivity.GoingToBuildDelivery;
            pawn.SetRoute(cell, route);
            availabilityRevision++;
        }

        private void ProgressCare(ColonistState worker)
        {
            var patient = colonistsById[worker.WorkTargetId.Value];
            if (worker != patient && TileDistance(worker.Position, patient.Position) != 1)
            { FinishProduction(worker); return; }
            if (++worker.CareProgress < RecoveryDuration) return;
            worker.CargoAmount = 0;
            MedicineConsumed++;
            patient.Health = Math.Min(100, patient.Health + 40);
            ReleaseRescuedBed(patient);
            cancelledThisTick.Add(patient.Id);
            FinishProduction(worker);
        }

        private void DeliverRepairMaterial(ColonistState pawn)
        {
            var site = Building(pawn.WorkTargetId.Value);
            if (TileDistance(pawn.Position, site.Position) != 1) { FinishProduction(pawn); return; }
            site.RepairWood += pawn.CargoAmount;
            pawn.CargoAmount = 0;
            FinishPriorityStage(pawn);
        }

        private void ProgressRepair(ColonistState worker)
        {
            var site = Building(worker.WorkTargetId.Value);
            if (TileDistance(worker.Position, site.Position) != 1) { FinishProduction(worker); return; }
            if (site.RepairProgress < RecoveryDuration - 1) { site.RepairProgress++; return; }
            var closingWall = site.Durability == 0 && ConstructionRules.BlocksMovement(site.Kind);
            if (closingWall && (IsOccupied(site.Position) || Colonists.Any(pawn => pawn.Path.Count > 0 && pawn.Path[0] == site.Position) ||
                raiders.Any(raider => raider.NextCell == site.Position)))
            { worker.WorkReason = WorkReasonCode.AwaitingClearance.ToString(); return; }
            site.RepairProgress = 0;
            site.RepairWood = 0;
            RepairWoodConsumed += 2;
            site.Durability = Math.Min(100, site.Durability + 40);
            if (site.Kind == BuildingKind.Bed) bedsById[site.Id].SetExists(true);
            if (closingWall) SetProductionBlocked(site.Position, true);
            if (site.Durability == 100) site.RepairDesignated = false;
            FinishPriorityStage(worker);
        }
    }
}
