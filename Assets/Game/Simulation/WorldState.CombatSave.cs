using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace RimworldTweak.Simulation
{
    [DataContract]
    public sealed class RaidSaveData
    {
        [DataMember(EmitDefaultValue = false)] public string Kind;
        [DataMember(EmitDefaultValue = false)] public int? RetargetPercent;
        [DataMember(EmitDefaultValue = false)] public uint? RetargetState;
        [DataMember(IsRequired = true)] public string Phase;
        [DataMember(IsRequired = true)] public string Outcome;
        [DataMember(IsRequired = true)] public int Remaining;
        [DataMember(IsRequired = true)] public int? ObjectiveId;
        [DataMember(IsRequired = true)] public List<int> Injured;
        [DataMember(IsRequired = true)] public List<int> Downed;
        [DataMember(IsRequired = true)] public int FacilityDamage;
        [DataMember(IsRequired = true)] public int ForcedRetreats;
        [DataMember(IsRequired = true)] public List<RaiderSaveData> Raiders;
    }

    [DataContract]
    public sealed class RaiderSaveData
    {
        [DataMember(IsRequired = true)] public int Id;
        [DataMember(IsRequired = true)] public int Health;
        [DataMember(IsRequired = true)] public PositionSaveData Position;
        [DataMember(IsRequired = true)] public PositionSaveData NextCell;
        [DataMember(IsRequired = true)] public int MoveProgress;
        [DataMember(IsRequired = true)] public long RetryTick;
        [DataMember(IsRequired = true)] public long RouteRevision;
        [DataMember(IsRequired = true)] public AttackSaveData Attack;
    }

    [DataContract]
    public sealed class AttackSaveData
    {
        [DataMember(IsRequired = true)] public int? Target;
        [DataMember(IsRequired = true)] public int Preparation;
        [DataMember(IsRequired = true)] public int Cooldown;
        [DataMember(IsRequired = true)] public string BlockedReason;
    }

    public sealed partial class WorldState
    {
        internal static AttackSaveData CaptureAttack(AttackState attack) => new AttackSaveData {
            Target = attack.TargetId, Preparation = attack.Preparation, Cooldown = attack.Cooldown,
            BlockedReason = attack.BlockedReason
        };

        internal static void RestoreAttack(AttackState attack, AttackSaveData data)
        {
            RequireSave(data != null && data.Preparation >= 0 && data.Preparation < PrototypeEquipment.Rifle.AimTicks &&
                data.Cooldown >= 0 && data.Cooldown <= PrototypeEquipment.Rifle.RecoveryTicks && data.BlockedReason != null &&
                (data.Target.HasValue || data.Preparation == 0) && (data.Preparation == 0 || data.Cooldown == 0));
            attack.TargetId = data.Target;
            attack.Preparation = data.Preparation;
            attack.Cooldown = data.Cooldown;
            attack.BlockedReason = data.BlockedReason;
        }

        private static void ValidateAttackTiming(AttackState attack, int aimTicks, int recoveryTicks)
        {
            RequireSave(attack.Preparation < System.Math.Max(1, aimTicks) && attack.Cooldown <= recoveryTicks);
        }

        private RaidSaveData CaptureRaid() => new RaidSaveData {
            Kind = NormalRaid ? "Normal" : null,
            RetargetPercent = NormalRaid ? RetargetPercent : (int?)null,
            RetargetState = NormalRaid ? RetargetState : (uint?)null,
            Phase = RaidPhase.ToString(), Outcome = RaidOutcome.ToString(), Remaining = RaidRemaining,
            ObjectiveId = RaidObjectiveId, Injured = raidInjured.OrderBy(id => id).ToList(),
            Downed = raidDowned.OrderBy(id => id).ToList(), FacilityDamage = RaidFacilityDamage,
            ForcedRetreats = RaidForcedRetreats, Raiders = raiders.Select(raider => new RaiderSaveData {
                Id = raider.Id, Health = raider.Health, Position = PositionSaveData.From(raider.Position),
                NextCell = raider.NextCell.HasValue ? PositionSaveData.From(raider.NextCell.Value) : null,
                MoveProgress = raider.MoveProgress, RetryTick = raider.RetryTick, RouteRevision = raider.RouteRevision,
                Attack = CaptureAttack(raider.Attack)
            }).ToList()
        };

        internal void ValidateInitialRaidStove(ScenarioDefinition scenario)
        {
            var initial = buildings.Where(building => building.InitialPlacement).ToList();
            RaidTrial = scenario.InitialRaidStove.HasValue;
            RequireSave(initial.Count == scenario.Beds.Count + (RaidTrial ? 1 : 0));
            foreach (var bed in scenario.Beds)
                RequireSave(initial.Any(site => site.Id == bed.Id && site.Kind == BuildingKind.Bed && site.Position == bed.Position));
            if (RaidTrial)
                RequireSave(initial.Any(site => site.Kind == BuildingKind.Stove && site.Position == scenario.InitialRaidStove.Value));
        }

        private void RestoreRaid(RaidSaveData data)
        {
            NormalRaid = data.Kind == "Normal";
            RetargetPercent = data.RetargetPercent.GetValueOrDefault();
            RetargetState = data.RetargetState.GetValueOrDefault();
            RaidPhase = SaveState.Enum<RaidPhase>(data.Phase);
            RaidOutcome = SaveState.Enum<RaidOutcome>(data.Outcome);
            RaidRemaining = data.Remaining;
            RaidObjectiveId = data.ObjectiveId;
            RaidFacilityDamage = data.FacilityDamage;
            RaidForcedRetreats = data.ForcedRetreats;
            RequireSave(RaidFacilityDamage >= 0 && RaidForcedRetreats >= 0 && RaidForcedRetreats <= 2);
            if (NormalRaid)
                RequireSave(!RaidObjectiveId.HasValue && RaidPhase != RaidPhase.Warning && RaidPhase != RaidPhase.Idle &&
                    RaidOutcome != RaidOutcome.ObjectiveLost && RaidFacilityDamage == 0);
            RequireSave(RaidRunning ? RaidRemaining > 0 && (NormalRaid || (RaidObjectiveId.HasValue &&
                Building(RaidObjectiveId.Value)?.Kind == BuildingKind.Stove)) : RaidRemaining == 0);
            var fighting = RaidPhase == RaidPhase.Active || RaidPhase == RaidPhase.Retreating;
            RequireSave(fighting ? data.Raiders.Count >= 1 && data.Raiders.Count <= 2 : data.Raiders.Count == 0);
            RequireSave(RaidPhase == RaidPhase.Warning || RaidPhase == RaidPhase.Active || RaidPhase == RaidPhase.Idle ?
                RaidOutcome == RaidOutcome.None : RaidOutcome != RaidOutcome.None);
            var maxRemaining = RaidPhase == RaidPhase.Warning ? RaidWarningTicks :
                RaidPhase == RaidPhase.Retreating ? RaidRetreatTicks : RaidCombatTicks;
            RequireSave(RaidRemaining <= maxRemaining);
            foreach (var id in data.Injured) RequireSave(colonistsById.ContainsKey(id) && raidInjured.Add(id));
            foreach (var id in data.Downed) RequireSave(raidInjured.Contains(id) && raidDowned.Add(id));
            foreach (var saved in data.Raiders)
            {
                var position = saved.Position.ToPosition();
                var next = saved.NextCell?.ToPosition();
                RequireSave(saved.Id > 0 && saved.Id <= nextEntityId && !raiders.Exists(x => x.Id == saved.Id) &&
                    !colonistsById.ContainsKey(saved.Id) && !resourcesById.ContainsKey(saved.Id) && !bedsById.ContainsKey(saved.Id) &&
                    blueprints.All(x => x.Id != saved.Id) && buildings.All(x => x.Id != saved.Id) &&
                    crops.All(x => x.Id != saved.Id) && growingZones.All(x => x.Id != saved.Id) &&
                    gatherables.All(x => x.Id != saved.Id) && cookingOrders.All(x => x.Id != saved.Id) &&
                    cookingOutputs.All(x => x.Id != saved.Id) &&
                    storageZones.All(x => x.Id != saved.Id && x.Slots.All(slot => slot.Id != saved.Id)));
                RequireSave(InBounds(position) && !blockedSet.Contains(position) && saved.Health > 0 && saved.Health <= 50 &&
                    saved.MoveProgress >= 0 && saved.MoveProgress < 5 && saved.RetryTick >= 0 &&
                    saved.RouteRevision >= -1 && saved.RouteRevision <= MapRevision &&
                    (next.HasValue ? InBounds(next.Value) && TileDistance(position, next.Value) == 1 : saved.MoveProgress == 0));
                var raider = new RaiderState(saved.Id, position) {
                    Health = saved.Health, NextCell = next, MoveProgress = saved.MoveProgress,
                    RetryTick = saved.RetryTick, RouteRevision = saved.RouteRevision
                };
                RestoreAttack(raider.Attack, saved.Attack);
                ValidateAttackTiming(raider.Attack, 10, 10);
                RequireSave(!raider.Attack.TargetId.HasValue || RaidPhase == RaidPhase.Active &&
                    (colonistsById.ContainsKey(raider.Attack.TargetId.Value) || RaidObjectiveId == raider.Attack.TargetId));
                if (NormalRaid && RaidPhase == RaidPhase.Active)
                    RequireSave(raider.Attack.TargetId is int targetId && colonistsById.TryGetValue(targetId, out var target) && !target.IsDowned);
                raiders.Add(raider);
            }
            raiders.Sort((first, second) => first.Id.CompareTo(second.Id));
            foreach (var pawn in Colonists)
            {
                RequireSave(!pawn.Attack.TargetId.HasValue || RaidPhase == RaidPhase.Active &&
                    !pawn.IsDowned && pawn.IsDrafted && raiders.Exists(x => x.Id == pawn.Attack.TargetId));
                RequireSave(!pawn.IsDowned || !pawn.IsDrafted && !pawn.IsWorking && !pawn.IsLifeBusy &&
                    !pawn.HasPendingCommand && pawn.Status != MovementStatus.Moving && pawn.CargoAmount == 0);
            }
        }
    }
}
