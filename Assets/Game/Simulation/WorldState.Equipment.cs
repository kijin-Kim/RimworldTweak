using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace RimworldTweak.Simulation
{
    public sealed class WeaponItemState
    {
        internal WeaponItemState(int id, WeaponDefinition definition, GridPosition position)
        {
            Id = id;
            Definition = definition;
            Position = position;
        }

        public int Id { get; }
        public WeaponDefinition Definition { get; }
        // Only ground items use Position. Equipped items follow their holder.
        public GridPosition Position { get; internal set; }
        public int? HolderId { get; internal set; }
        public int? ReservedById { get; internal set; }
    }

    [DataContract]
    public sealed class EquipmentSaveData
    {
        [DataMember(IsRequired = true)] public List<WeaponItemSaveData> Weapons;
    }

    [DataContract]
    public sealed class WeaponItemSaveData
    {
        [DataMember(IsRequired = true)] public int Id;
        [DataMember(IsRequired = true)] public string Definition;
        [DataMember(IsRequired = true)] public PositionSaveData GroundCell;
        [DataMember(IsRequired = true)] public int? HolderId;
        [DataMember(IsRequired = true)] public int? ReservedById;
    }

    public sealed partial class WorldState
    {
        private readonly List<WeaponItemState> weapons = new List<WeaponItemState>();
        public IReadOnlyList<WeaponItemState> Weapons => weapons.AsReadOnly();
        public bool EquipmentEnabled { get; private set; }
        public WeaponItemState EquippedWeapon(int pawnId) => weapons.Find(w => w.HolderId == pawnId);
        public bool HasEquipOrder(int pawnId) => weapons.Exists(w => w.ReservedById == pawnId);

        internal void SeedStartingWeapons()
        {
            if (EquipmentEnabled) return;
            EquipmentEnabled = true;
            var origin = Colonists[0].Position;
            foreach (var definition in new[] { PrototypeEquipment.Rifle, PrototypeEquipment.Knife })
            {
                var cell = origin;
                foreach (var step in Directions)
                {
                    var candidate = new GridPosition(origin.X + step.X, origin.Y + step.Y);
                    if (!InBounds(candidate) || blockedSet.Contains(candidate) || IsOccupied(candidate) ||
                        weapons.Exists(w => w.Position == candidate)) continue;
                    cell = candidate;
                    break;
                }
                weapons.Add(new WeaponItemState(NewId(), definition, cell));
            }
        }

        public bool OrderEquip(int pawnId, int weaponId)
        {
            if (!colonistsById.TryGetValue(pawnId, out var pawn)) return Reject("InvalidColonist");
            if (pawn.IsDowned) return Reject("ColonistDowned");
            var weapon = weapons.Find(w => w.Id == weaponId);
            if (weapon == null || weapon.HolderId.HasValue) return Reject("WeaponNotOnGround");
            if (weapon.ReservedById == pawnId) { LastCommandError = string.Empty; return true; }
            if (weapon.ReservedById.HasValue) return Reject("WeaponReserved");
            var path = ReadPath(pawn.Position, weapon.Position);
            if (path == null) return Reject("Unreachable");

            StopDirectOrders(pawnId);
            CancelPatientTreatment(pawn);
            EndLife(pawn);
            weapon.ReservedById = pawnId;
            pawn.SetRoute(weapon.Position, path);
            LastCommandError = string.Empty;
            return true;
        }

        internal void ReleaseEquipReservation(int pawnId)
        {
            var weapon = weapons.Find(w => w.ReservedById == pawnId);
            if (weapon != null) weapon.ReservedById = null;
        }

        public bool CancelEquip(int pawnId)
        {
            if (!HasEquipOrder(pawnId)) return Reject("NoEquipOrder");
            ReleaseEquipReservation(pawnId);
            colonistsById[pawnId].Stop();
            LastCommandError = string.Empty;
            return true;
        }

        internal void FinishEquipArrivals()
        {
            foreach (var item in weapons)
            {
                if (!(item.ReservedById is int owner)) continue;
                var pawn = colonistsById[owner];
                if (pawn.IsDowned || pawn.Destination != item.Position || pawn.Status == MovementStatus.Blocked)
                {
                    ReleaseEquipReservation(owner);
                    if (pawn.Destination == item.Position) pawn.Stop();
                    continue;
                }
                if (pawn.Position != item.Position) continue;
                var previous = EquippedWeapon(owner);
                if (previous != null)
                {
                    previous.Position = pawn.Position;
                    previous.HolderId = null;
                }
                item.HolderId = owner;
                item.ReservedById = null;
                pawn.Attack.Cancel();
                pawn.Attack.Cooldown = 0;
                pawn.Stop();
            }
        }

        internal EquipmentSaveData CaptureEquipment() => !EquipmentEnabled ? null : new EquipmentSaveData {
            Weapons = weapons.Select(w => new WeaponItemSaveData {
                Id = w.Id, Definition = w.Definition.Id, GroundCell = w.HolderId.HasValue ? null : PositionSaveData.From(w.Position),
                HolderId = w.HolderId, ReservedById = w.ReservedById
            }).ToList()
        };

        internal void RestoreEquipment(EquipmentSaveData data)
        {
            EquipmentEnabled = data != null;
            if (EquipmentEnabled)
            {
                RequireSave(data.Weapons != null && data.Weapons.Count == 2);
                var definitions = new HashSet<string>();
                foreach (var entry in data.Weapons)
                {
                    RequireSave(entry != null && definitions.Add(entry.Definition) && entry.Id > 0 && entry.Id <= nextEntityId &&
                        !weapons.Exists(w => w.Id == entry.Id) && !WorldUsesItemId(entry.Id));
                    var definition = entry.Definition == "rifle" ? PrototypeEquipment.Rifle :
                        entry.Definition == "knife" ? PrototypeEquipment.Knife : null;
                    RequireSave(definition != null && entry.HolderId.HasValue == (entry.GroundCell == null));
                    var position = entry.GroundCell?.ToPosition() ?? default;
                    if (entry.HolderId is int holder)
                    {
                        RequireSave(colonistsById.ContainsKey(holder) && EquippedWeapon(holder) == null && !entry.ReservedById.HasValue);
                        position = colonistsById[holder].Position;
                    }
                    else RequireSave(InBounds(position));
                    if (entry.ReservedById is int owner)
                    {
                        RequireSave(colonistsById.TryGetValue(owner, out var pawn) && !pawn.IsDowned && !HasEquipOrder(owner) &&
                            !pawn.IsLifeBusy && !pawn.IsWorking && !pawn.Attack.TargetId.HasValue && !pawn.HasPendingCommand &&
                            pawn.Destination == position && (pawn.Status == MovementStatus.Moving || pawn.Status == MovementStatus.Arrived));
                    }
                    weapons.Add(new WeaponItemState(entry.Id, definition, position) { HolderId = entry.HolderId, ReservedById = entry.ReservedById });
                }
            }
            foreach (var pawn in Colonists)
            {
                var definition = EquippedWeapon(pawn.Id)?.Definition;
                RequireSave(!EquipmentEnabled || definition != null || !pawn.Attack.TargetId.HasValue);
                ValidateAttackTiming(pawn.Attack, definition?.AimTicks ?? (EquipmentEnabled ? 0 : 10),
                    definition?.RecoveryTicks ?? (EquipmentEnabled ? 0 : 10));
            }
        }

        private bool WorldUsesItemId(int id) => colonistsById.ContainsKey(id) || resourcesById.ContainsKey(id) ||
            bedsById.ContainsKey(id) || raiders.Exists(r => r.Id == id) || blueprints.Exists(b => b.Id == id) ||
            buildings.Exists(b => b.Id == id) || crops.Exists(c => c.Id == id) || growingZones.Exists(z => z.Id == id) ||
            gatherables.Exists(g => g.Id == id) || cookingOrders.Exists(o => o.Id == id) || cookingOutputs.Exists(o => o.Id == id) ||
            storageZones.Exists(z => z.Id == id || z.Slots.Any(s => s.Id == id));
    }
}
