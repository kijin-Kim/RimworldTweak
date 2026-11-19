using System;
using System.Linq;
using System.Collections.Generic;
namespace RimworldTweak.Simulation
{
    internal static class SaveState
    {
        internal static T Enum<T>(string value) where T:struct => (T)System.Enum.Parse(typeof(T),value);
        internal static ClaimSaveData Capture(InteractionClaim claim) => new ClaimSaveData{OwnerId=claim.OwnerId,ActionId=claim.ActionId};
        internal static void Restore(InteractionClaim claim,ClaimSaveData data)
        {claim.Clear();if(data.OwnerId.HasValue)claim.Acquire(data.OwnerId.Value,data.ActionId);}
    }
    public sealed partial class ColonistState
    {
        internal ColonistSaveData CaptureSave() => new ColonistSaveData
        {Id=Id,
            Position=PositionSaveData.From(Position),
            Destination=Destination.HasValue?PositionSaveData.From(Destination.Value):null,
            Path=path.Select(PositionSaveData.From).ToList(),
            MovementProgress=MovementProgress,
            MovementTicksPerTile=MovementTicksPerTile,
            Status=Status.ToString(),
            HasPendingCommand=HasPendingCommand,
            PendingDestination=PendingDestination.HasValue?PositionSaveData.From(PendingDestination.Value):null,
            Health=Health,
            CareProgress=CareProgress,
            PatientClaim=SaveState.Capture(PatientClaim),
            Attack=WorldState.CaptureAttack(Attack),
            IsDrafted=IsDrafted,
            ActionId=ActionId,
            HasSleepIntent=HasSleepIntent,
            WorkActivity=WorkActivity.ToString(),
            WorkTargetId=WorkTargetId,
            IsCookingDelivery=IsCookingDelivery,
            FeatureStep=FeatureStep?.ToString(),
            PendingDeliveryTargetId=PendingDeliveryTargetId,
            CargoKind=CargoKind.ToString(),
            CargoAmount=CargoAmount,
            WorkReason=WorkReason,
            WorkReadyTick=WorkReadyTick,
            NextWorkSearchTick=NextWorkSearchTick,
            LastWorkRevision=LastWorkRevision,
            NextBedSearchTick=NextBedSearchTick,
            LastBedRevision=LastBedRevision,
            LastBedPosition=PositionSaveData.From(LastBedPosition),
            Nutrition=Nutrition,
            Rest=Rest,
            LifeActivity=LifeActivity.ToString(),
            LifeTargetId=LifeTargetId,
            ActionProgress=ActionProgress,
            ActionDuration=ActionDuration,
            FoodReason=FoodReason,
            SleepReason=SleepReason,
            MealsEaten=MealsEaten,
            RawMealsEaten=RawMealsEaten,
            SleepsCompleted=SleepsCompleted,
            StarvationTicks=StarvationTicks,
            NextFoodSearchTick=NextFoodSearchTick,
            LastFoodRevision=LastFoodRevision,
            LastFoodPosition=PositionSaveData.From(LastFoodPosition)};
        internal static ColonistState RestoreSave(ColonistSaveData d)
        {
            var v=new ColonistState(d.Id,d.Position.ToPosition());
            v.Position=d.Position.ToPosition();
            v.Destination=d.Destination?.ToPosition();
            v.path.AddRange(d.Path.Select(x=>x.ToPosition()));
            v.MovementProgress=d.MovementProgress;
            v.MovementTicksPerTile=d.MovementTicksPerTile;
            v.Status=SaveState.Enum<MovementStatus>(d.Status);
            v.HasPendingCommand=d.HasPendingCommand;
            v.PendingDestination=d.PendingDestination?.ToPosition();
            if (d.Health < 0 || d.Health > 100) throw new ArgumentException("SaveInvalidData");
            v.Health=d.Health;
            v.CareProgress=d.CareProgress;
            SaveState.Restore(v.PatientClaim,d.PatientClaim);
            WorldState.RestoreAttack(v.Attack,d.Attack);
            v.IsDrafted=d.IsDrafted;
            v.ActionId=d.ActionId;
            v.HasSleepIntent=d.HasSleepIntent;
            v.WorkActivity=SaveState.Enum<WorkActivity>(d.WorkActivity);
            v.WorkTargetId=d.WorkTargetId;
            v.IsCookingDelivery=d.IsCookingDelivery;
            v.FeatureStep=d.FeatureStep==null?(WorkStep?)null:SaveState.Enum<WorkStep>(d.FeatureStep);
            v.PendingDeliveryTargetId=d.PendingDeliveryTargetId;
            v.CargoKind=SaveState.Enum<ResourceKind>(d.CargoKind);
            v.CargoAmount=d.CargoAmount;
            v.WorkReason=d.WorkReason;
            v.WorkReadyTick=d.WorkReadyTick;
            v.NextWorkSearchTick=d.NextWorkSearchTick;
            v.LastWorkRevision=d.LastWorkRevision;
            v.NextBedSearchTick=d.NextBedSearchTick;
            v.LastBedRevision=d.LastBedRevision;
            v.LastBedPosition=d.LastBedPosition.ToPosition();
            v.Nutrition=d.Nutrition;
            v.Rest=d.Rest;
            v.LifeActivity=SaveState.Enum<LifeActivity>(d.LifeActivity);
            v.LifeTargetId=d.LifeTargetId;
            v.ActionProgress=d.ActionProgress;
            v.ActionDuration=d.ActionDuration;
            v.FoodReason=d.FoodReason;
            v.SleepReason=d.SleepReason;
            v.MealsEaten=d.MealsEaten;
            v.RawMealsEaten=d.RawMealsEaten;
            v.SleepsCompleted=d.SleepsCompleted;
            v.StarvationTicks=d.StarvationTicks;
            v.NextFoodSearchTick=d.NextFoodSearchTick;
            v.LastFoodRevision=d.LastFoodRevision;
            v.LastFoodPosition=d.LastFoodPosition.ToPosition();
            return v;
        }
    }
    public sealed partial class ResourceState
    {
        internal ResourceSaveData CaptureSave() => new ResourceSaveData
        {Id=Id,
            Kind=Kind.ToString(),
            Position=PositionSaveData.From(Position),
            Amount=Amount,
            StorageSlotId=StorageSlotId,
            CreatedTick=CreatedTick,
            Claim=SaveState.Capture(Claim),
            IsInUse=IsInUse,
            Exists=Exists,
            ConsumedAmount=ConsumedAmount,
            AddedAmount=AddedAmount,
            RemovedAmount=RemovedAmount,
            Reservations=reservations.Select(x=>new ReservationSaveData{OwnerId=x.Key,Amount=x.Value}).ToList()};
        internal static ResourceState RestoreSave(ResourceSaveData d)
        {
            var v=new ResourceState(new ResourceSeed(d.Id,SaveState.Enum<ResourceKind>(d.Kind),d.Amount,d.Position.ToPosition()));
            v.Amount=d.Amount;
            v.StorageSlotId=d.StorageSlotId;
            v.CreatedTick=d.CreatedTick;
            SaveState.Restore(v.Claim,d.Claim);
            v.IsInUse=d.IsInUse;
            v.Exists=d.Exists;
            v.ConsumedAmount=d.ConsumedAmount;
            v.AddedAmount=d.AddedAmount;
            v.RemovedAmount=d.RemovedAmount;
            foreach(var entry in d.Reservations)v.reservations.Add(entry.OwnerId,entry.Amount);
            return v;
        }
    }
    public sealed partial class BedState
    {
        internal BedSaveData CaptureSave() => new BedSaveData
        {Id=Id,
            Position=PositionSaveData.From(Position),
            Exists=Exists,
            IsOccupied=IsOccupied,
            Claim=new ClaimSaveData{OwnerId=OwnerId,ActionId=OwnerActionId}};
        internal static BedState RestoreSave(BedSaveData d)
        {
            var v=new BedState(new BedSeed(d.Id,d.Position.ToPosition()));
            v.Exists=d.Exists;
            v.IsOccupied=d.IsOccupied;
            v.OwnerId=d.Claim.OwnerId;v.OwnerActionId=d.Claim.ActionId;
            return v;
        }
    }
    public sealed partial class GatherState
    {
        internal GatherSaveData CaptureSave() => new GatherSaveData
        {Id=Id,
            Kind=Kind.ToString(),
            Position=PositionSaveData.From(Position),
            Designated=Designated,
            Depleted=Depleted,
            Progress=Progress,
            RequestOrder=RequestOrder,
            Claim=SaveState.Capture(Claim)};
        internal static GatherState RestoreSave(GatherSaveData d)
        {
            var v=new GatherState(new GatherSeed(d.Id,SaveState.Enum<GatherKind>(d.Kind),d.Position.ToPosition()));
            v.Designated=d.Designated;
            v.Depleted=d.Depleted;
            v.Progress=d.Progress;
            v.RequestOrder=d.RequestOrder;
            SaveState.Restore(v.Claim,d.Claim);
            return v;
        }
    }
    public sealed partial class StorageSlotState
    {
        internal StorageSlotSaveData CaptureSave() => new StorageSlotSaveData
        {Id=Id,
            ZoneId=ZoneId,
            Position=PositionSaveData.From(Position),
            StackId=Stack?.Id,
            Claim=SaveState.Capture(Claim)};
        internal static StorageSlotState RestoreSave(StorageSlotSaveData d)
        {
            var v=new StorageSlotState(d.Id,d.ZoneId,d.Position.ToPosition());
            SaveState.Restore(v.Claim,d.Claim);
            return v;
        }
    }
    public sealed partial class StorageZoneState
    {
        internal StorageZoneSaveData CaptureSave() => new StorageZoneSaveData
        {Id=Id,
            Exists=Exists,
            Allowed=allowed.OrderBy(x=>x).Select(x=>x.ToString()).ToList(),
            Slots=Slots.Select(x=>x.CaptureSave()).ToList()};
        internal static StorageZoneState RestoreSave(StorageZoneSaveData d,List<StorageSlotState> slots)
        {
            var v=new StorageZoneState(d.Id,slots);
            v.Exists=d.Exists;
            v.allowed.Clear();foreach(var kind in d.Allowed)v.allowed.Add(SaveState.Enum<ResourceKind>(kind));
            return v;
        }
    }
    public sealed partial class BlueprintState
    {
        internal BlueprintSaveData CaptureSave() => new BlueprintSaveData
        {Id=Id,
            Kind=Kind.ToString(),
            Position=PositionSaveData.From(Position),
            RequestOrder=RequestOrder,
            Exists=Exists,
            DeliveredWood=DeliveredWood,
            Progress=Progress,
            Reason=Reason.ToString(),
            Claim=SaveState.Capture(Claim)};
        internal static BlueprintState RestoreSave(BlueprintSaveData d)
        {
            var v=new BlueprintState(d.Id,SaveState.Enum<BuildingKind>(d.Kind),d.Position.ToPosition(),d.RequestOrder);
            v.RequestOrder=d.RequestOrder;
            v.Exists=d.Exists;
            v.DeliveredWood=d.DeliveredWood;
            v.Progress=d.Progress;
            v.Reason=SaveState.Enum<WorkReasonCode>(d.Reason);
            SaveState.Restore(v.Claim,d.Claim);
            return v;
        }
    }
    public sealed partial class BuildingState
    {
        internal BuildingSaveData CaptureSave() => new BuildingSaveData
        {Id=Id,
            Kind=Kind.ToString(),
            Position=PositionSaveData.From(Position),
            Exists=Exists,
            Durability=Durability,
            InitialPlacement=InitialPlacement,
            RepairDesignated=RepairDesignated,
            RepairWood=RepairWood,
            RepairProgress=RepairProgress,
            DemolitionDesignated=DemolitionDesignated,
            DemolitionProgress=DemolitionProgress,
            RequestOrder=RequestOrder,
            Reason=Reason.ToString(),
            Claim=SaveState.Capture(Claim)};
        internal static BuildingState RestoreSave(BuildingSaveData d)
        {
            var v=new BuildingState(d.Id,SaveState.Enum<BuildingKind>(d.Kind),d.Position.ToPosition());
            v.Exists=d.Exists;
            if (d.Durability < 0 || d.Durability > 100) throw new ArgumentException("SaveInvalidData");
            v.Durability=d.Durability;
            v.InitialPlacement=d.InitialPlacement;
            v.RepairDesignated=d.RepairDesignated;
            v.RepairWood=d.RepairWood;
            v.RepairProgress=d.RepairProgress;
            v.DemolitionDesignated=d.DemolitionDesignated;
            v.DemolitionProgress=d.DemolitionProgress;
            v.RequestOrder=d.RequestOrder;
            v.Reason=SaveState.Enum<WorkReasonCode>(d.Reason);
            SaveState.Restore(v.Claim,d.Claim);
            return v;
        }
    }
    public sealed partial class CropState
    {
        internal CropSaveData CaptureSave() => new CropSaveData
        {Id=Id,
            ZoneId=ZoneId,
            Position=PositionSaveData.From(Position),
            Exists=Exists,
            Stage=Stage.ToString(),
            SowProgress=SowProgress,
            HarvestProgress=HarvestProgress,
            GrowthTicks=GrowthTicks,
            PlantedTick=PlantedTick,
            RequestOrder=RequestOrder,
            Reason=Reason.ToString(),
            Claim=SaveState.Capture(Claim)};
        internal static CropState RestoreSave(CropSaveData d)
        {
            var v=new CropState(d.Id,d.ZoneId,d.Position.ToPosition(),d.RequestOrder);
            v.Exists=d.Exists;
            v.Stage=SaveState.Enum<CropStage>(d.Stage);
            v.SowProgress=d.SowProgress;
            v.HarvestProgress=d.HarvestProgress;
            v.GrowthTicks=d.GrowthTicks;
            v.PlantedTick=d.PlantedTick;
            v.RequestOrder=d.RequestOrder;
            v.Reason=SaveState.Enum<WorkReasonCode>(d.Reason);
            SaveState.Restore(v.Claim,d.Claim);
            return v;
        }
    }
    public sealed partial class GrowingZoneState
    {
        internal GrowingZoneSaveData CaptureSave() => new GrowingZoneSaveData
        {Id=Id,
            Exists=Exists,
            SowingEnabled=SowingEnabled,
            CellIds=Cells.Select(x=>x.Id).ToList()};
        internal static GrowingZoneState RestoreSave(GrowingZoneSaveData d,List<CropState> cells)
        {
            var v=new GrowingZoneState(d.Id,cells);
            v.Exists=d.Exists;
            v.SowingEnabled=d.SowingEnabled;
            return v;
        }
    }
    public sealed partial class CookingOrderState
    {
        internal CookingOrderSaveData CaptureSave() => new CookingOrderSaveData
        {Id=Id,
            RepeatMode=(int)RepeatMode, RemainingCount=RemainingCount, Priority=Priority,
            WorkerId=WorkerId, AllowIngredients=AllowIngredients, IngredientRadius=IngredientRadius,
            DeliveryMode=(int)DeliveryMode, DestinationZoneId=DestinationZoneId,
            StoveId=StoveId,
            ServiceCell=PositionSaveData.From(ServiceCell),
            Exists=Exists,
            Enabled=Enabled,
            TargetMeals=TargetMeals,
            InputIngredients=InputIngredients,
            Progress=Progress,
            ActiveCycle=ActiveCycle,
            RequestOrder=RequestOrder,
            Reason=Reason.ToString(),
            CompletedMeals=CompletedMeals,
            Claim=SaveState.Capture(Claim)};
        internal static CookingOrderState RestoreSave(CookingOrderSaveData d,InteractionClaim claim)
        {
            var v=new CookingOrderState(d.Id,d.StoveId,d.ServiceCell.ToPosition(),claim);
            v.RepeatMode=(CookingRepeatMode)d.RepeatMode; v.RemainingCount=d.RemainingCount;
            v.Priority=d.Priority; v.WorkerId=d.WorkerId; v.AllowIngredients=d.AllowIngredients??true;
            v.IngredientRadius=d.IngredientRadius??999;
            v.DeliveryMode=(CookingDeliveryMode)d.DeliveryMode; v.DestinationZoneId=d.DestinationZoneId;
            v.Exists=d.Exists;
            v.Enabled=d.Enabled;
            v.TargetMeals=d.TargetMeals;
            v.InputIngredients=d.InputIngredients;
            v.Progress=d.Progress;
            v.ActiveCycle=d.ActiveCycle;
            v.RequestOrder=d.RequestOrder;
            v.Reason=SaveState.Enum<WorkReasonCode>(d.Reason);
            v.CompletedMeals=d.CompletedMeals;
            SaveState.Restore(v.Claim,d.Claim);
            return v;
        }
    }
    public sealed partial class CookingOutputState
    {
        internal CookingOutputSaveData CaptureSave() => new CookingOutputSaveData
        {Id=Id,
            StoveId=StoveId,
            Position=PositionSaveData.From(Position),
            Exists=Exists,
            StackId=Stack?.Id,
            Claim=SaveState.Capture(Claim)};
        internal static CookingOutputState RestoreSave(CookingOutputSaveData d)
        {
            var v=new CookingOutputState(d.Id,d.StoveId,d.Position.ToPosition());
            v.Exists=d.Exists;
            SaveState.Restore(v.Claim,d.Claim);
            return v;
        }
    }
}
