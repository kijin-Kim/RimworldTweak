using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace RimworldTweak.Simulation
{
    public sealed class RescueState
    {
        internal RescueState(int workerId, int patientId, int bedId, bool carrying = false)
        { WorkerId = workerId; PatientId = patientId; BedId = bedId; Carrying = carrying; }

        public int WorkerId { get; }
        public int PatientId { get; }
        public int BedId { get; }
        public bool Carrying { get; internal set; }
    }

    [DataContract]
    public sealed class RescueSaveData
    {
        // WorkerId is absent after handoff, when the patient owns the occupied bed.
        [DataMember(IsRequired = true)] public int? WorkerId;
        [DataMember(IsRequired = true)] public int PatientId;
        [DataMember(IsRequired = true)] public int BedId;
        [DataMember(IsRequired = true)] public bool Carrying;
    }

    public sealed partial class ColonistState
    {
        internal void MoveWithRescuer(GridPosition position) => Position = position;
    }

    public sealed partial class WorldState
    {
        private readonly List<RescueState> activeRescues = new List<RescueState>();
        private readonly Dictionary<int,int> rescuedBeds = new Dictionary<int,int>();

        public RescueState RescueFor(int workerId) => activeRescues.Find(r => r.WorkerId == workerId);
        public int? RescuerFor(int patientId) => activeRescues.Find(r => r.PatientId == patientId)?.WorkerId;
        public int? RescuedBedFor(int patientId) => rescuedBeds.TryGetValue(patientId, out var bedId) ? bedId : (int?)null;
        public bool HasRescueCommand(int workerId) => RescueFor(workerId) != null;

        public string RescueReason(int workerId, int patientId)
            => PlanRescue(workerId, patientId, out _, out _);

        private string PlanRescue(int workerId, int patientId, out BedState selected, out IReadOnlyList<GridPosition> pickup)
        {
            selected = null;
            pickup = null;
            if (!colonistsById.TryGetValue(workerId, out var worker) || !colonistsById.TryGetValue(patientId, out var patient))
                return "InvalidColonist";
            if (worker == patient || worker.IsDowned || worker.IsDrafted || worker.TreatedBy.HasValue) return "RescuerUnavailable";
            if (!patient.IsDowned) return "RescueRequiresDowned";
            if (RescuedBedFor(patientId).HasValue) return "PatientInBed";
            if (patient.TreatedBy.HasValue || RescuerFor(patientId).HasValue) return "PatientReserved";
            pickup = ReadPath(worker.Position, patient.Position);
            if (pickup == null) return "Unreachable";
            var previous = RescueFor(workerId);
            var distance = int.MaxValue;
            var available = false;
            foreach (var bed in Beds)
            {
                if (!bed.Exists || bed.IsOccupied || (bed.OwnerId.HasValue && bed.Id != previous?.BedId)) continue;
                available = true;
                var route = ReadPath(patient.Position, bed.Position);
                if (route == null || route.Count > distance ||
                    (route.Count == distance && selected != null && bed.Id >= selected.Id)) continue;
                distance = route.Count;
                selected = bed;
            }
            return selected != null ? string.Empty : available ? "Unreachable" : "NoRescueBed";
        }

        public bool OrderRescue(int workerId, int patientId)
        {
            var reason = PlanRescue(workerId, patientId, out var bed, out var pickup);
            if (reason.Length != 0) return Reject(reason);
            var worker = colonistsById[workerId];
            StopDirectOrders(workerId);
            EndLife(worker);
            bed.Reserve(workerId, worker.ActionId);
            activeRescues.Add(new RescueState(workerId, patientId, bed.Id));
            worker.SetRoute(colonistsById[patientId].Position, pickup);
            availabilityRevision++;
            LastCommandError = string.Empty;
            return true;
        }

        public bool CancelRescue(int workerId)
        {
            if (!HasRescueCommand(workerId)) return Reject("NoRescueCommand");
            CancelRescueForPawn(workerId);
            LastCommandError = string.Empty;
            return true;
        }

        private void CancelRescueForPawn(int id)
        {
            var rescue = activeRescues.Find(r => r.WorkerId == id || r.PatientId == id);
            if (rescue == null)
            {
                if (rescuedBeds.ContainsKey(id)) ReleaseRescuedBed(colonistsById[id]);
                return;
            }
            var worker = colonistsById[rescue.WorkerId];
            if (rescue.Carrying) colonistsById[rescue.PatientId].MoveWithRescuer(worker.Position);
            bedsById[rescue.BedId].Release(worker.Id, worker.ActionId);
            worker.Stop();
            activeRescues.Remove(rescue);
            availabilityRevision++;
        }

        private void ReleaseRescuedBed(ColonistState patient)
        {
            if (!rescuedBeds.TryGetValue(patient.Id, out var id)) return;
            bedsById[id].Release(patient.Id, patient.ActionId);
            rescuedBeds.Remove(patient.Id);
            availabilityRevision++;
        }

        private void InvalidateRescueBed(int bedId)
        {
            var rescue = activeRescues.Find(r => r.BedId == bedId);
            if (rescue != null) CancelRescueForPawn(rescue.WorkerId);
            foreach (var entry in rescuedBeds)
                if (entry.Value == bedId)
                {
                    ReleaseRescuedBed(colonistsById[entry.Key]);
                    break;
                }
        }

        // Called after pawn movement, before combat. A carried pawn always occupies its worker's logical cell.
        private void AdvanceRescues()
        {
            if (rescuedBeds.Count != 0)
                foreach (var entry in rescuedBeds.ToArray())
                    if (!bedsById[entry.Value].Exists || !colonistsById[entry.Key].IsDowned)
                        ReleaseRescuedBed(colonistsById[entry.Key]);

            for (var index = activeRescues.Count - 1; index >= 0; index--)
            {
                var rescue = activeRescues[index];
                var worker = colonistsById[rescue.WorkerId];
                var patient = colonistsById[rescue.PatientId];
                var bed = bedsById[rescue.BedId];
                if (!bed.Exists || bed.OwnerId != worker.Id || worker.IsDowned || worker.IsDrafted ||
                    !patient.IsDowned || worker.Status == MovementStatus.Blocked)
                { CancelRescueForPawn(worker.Id); continue; }
                if (!rescue.Carrying)
                {
                    if (worker.Position != patient.Position) continue;
                    var route = ReadPath(worker.Position, bed.Position);
                    if (route == null) { CancelRescueForPawn(worker.Id); continue; }
                    rescue.Carrying = true;
                    worker.SetRoute(bed.Position, route);
                }
                patient.MoveWithRescuer(worker.Position);
                if (worker.Position != bed.Position) continue;
                bed.Release(worker.Id, worker.ActionId);
                bed.Reserve(patient.Id, patient.ActionId);
                bed.Occupy(patient.Id, patient.ActionId);
                rescuedBeds.Add(patient.Id, bed.Id);
                activeRescues.RemoveAt(index);
                worker.Stop();
                availabilityRevision++;
            }
        }

        private bool IsRescueBedClaim(BedState bed) =>
            activeRescues.Exists(r => r.BedId == bed.Id) ||
            (bed.OwnerId.HasValue && RescuedBedFor(bed.OwnerId.Value) == bed.Id);

        private List<RescueSaveData> CaptureRescues()
        {
            if (activeRescues.Count == 0 && rescuedBeds.Count == 0) return null;
            var data = activeRescues.OrderBy(r => r.WorkerId).Select(r => new RescueSaveData {
                WorkerId = r.WorkerId, PatientId = r.PatientId, BedId = r.BedId, Carrying = r.Carrying
            }).ToList();
            foreach (var entry in rescuedBeds.OrderBy(pair => pair.Key))
                data.Add(new RescueSaveData { PatientId = entry.Key, BedId = entry.Value });
            return data;
        }

        private void RestoreRescues(List<RescueSaveData> records)
        {
            if (records == null) return;
            RequireSave(records.Count <= Colonists.Count);
            var patients = new HashSet<int>();
            var reservedBeds = new HashSet<int>();
            foreach (var item in records)
            {
                RequireSave(item != null && patients.Add(item.PatientId) && reservedBeds.Add(item.BedId));
                RequireSave(colonistsById.ContainsKey(item.PatientId) && bedsById.ContainsKey(item.BedId));
                var patient = colonistsById[item.PatientId];
                var bed = bedsById[item.BedId];
                RequireSave(bed.Exists && patient.IsDowned && !patient.IsWorking && !patient.IsLifeBusy &&
                    !patient.IsDrafted && !patient.HasPendingCommand && patient.Status != MovementStatus.Moving);
                if (item.WorkerId.HasValue)
                {
                    RequireSave(colonistsById.ContainsKey(item.WorkerId.Value));
                    var worker = colonistsById[item.WorkerId.Value];
                    RequireSave(!worker.IsDowned && !worker.IsDrafted && !worker.IsWorking && !worker.IsLifeBusy &&
                        !worker.HasPendingCommand && !worker.TreatedBy.HasValue && !patient.TreatedBy.HasValue &&
                        !HasRescueCommand(worker.Id) && bed.OwnerId == worker.Id && bed.OwnerActionId == worker.ActionId && !bed.IsOccupied &&
                        (!item.Carrying || patient.Position == worker.Position) &&
                        worker.Destination == (item.Carrying ? bed.Position : patient.Position) &&
                        (worker.Status == MovementStatus.Moving || worker.Status == MovementStatus.Arrived));
                    activeRescues.Add(new RescueState(worker.Id, patient.Id, bed.Id, item.Carrying));
                }
                else
                {
                    RequireSave(!item.Carrying && bed.IsOccupied && bed.OwnerId == patient.Id &&
                        bed.OwnerActionId == patient.ActionId && bed.Position == patient.Position);
                    rescuedBeds.Add(patient.Id, bed.Id);
                }
            }
        }
    }
}
