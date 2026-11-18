using System.Collections.Generic;
using System.Runtime.Serialization;

namespace RimworldTweak.Simulation
{
    [DataContract]
    public sealed class SaveData
    {
        [DataMember(Name="schemaVersion", IsRequired=true)] public int SchemaVersion;
        [DataMember(Name="scenarioId", IsRequired=true)] public string ScenarioId;
        [DataMember(Name="scenarioVersion", IsRequired=true)] public int ScenarioVersion;
        [DataMember(Name="contractVersion", IsRequired=true)] public string ContractVersion;
        [DataMember(Name="rulesVersion", IsRequired=true)] public int RulesVersion;
        [DataMember(Name="session", IsRequired=true)] public SessionSaveData Session;
        [DataMember(Name="scenario", IsRequired=true)] public ScenarioSaveData Scenario;
        [DataMember(Name="world", IsRequired=true)] public WorldSaveData World;
    }
    [DataContract]
    public sealed class SessionSaveData
    {
        [DataMember(Name="ResetCount", IsRequired=true)] public int ResetCount;
        [DataMember(Name="PendingSeconds", IsRequired=true)] public string PendingSeconds;
        [DataMember(Name="Paused", IsRequired=true)] public bool Paused;
        [DataMember(Name="Speed", IsRequired=true)] public int Speed;
    }
    [DataContract]
    public sealed class PositionSaveData
    {
        [DataMember(Name="X", IsRequired=true)] public int X;
        [DataMember(Name="Y", IsRequired=true)] public int Y;
        public PositionSaveData() { }
        public PositionSaveData(int x,int y) { X=x;Y=y; }
        public static PositionSaveData From(GridPosition value) => new PositionSaveData(value.X,value.Y);
        public GridPosition ToPosition() => new GridPosition(X,Y);
    }
    [DataContract]
    public sealed class ClaimSaveData
    {
        [DataMember(Name="OwnerId", IsRequired=true)] public int? OwnerId;
        [DataMember(Name="ActionId", IsRequired=true)] public long ActionId;
    }
    [DataContract]
    public sealed class ReservationSaveData
    {
        [DataMember(Name="OwnerId", IsRequired=true)] public int OwnerId;
        [DataMember(Name="Amount", IsRequired=true)] public int Amount;
    }
    [DataContract]
    public sealed class ResourceCountSaveData
    {
        [DataMember(Name="Kind", IsRequired=true)] public string Kind;
        [DataMember(Name="Amount", IsRequired=true)] public int Amount;
    }
    [DataContract]
    public sealed class ScenarioSaveData
    {
        [DataMember(Name="Width", IsRequired=true)] public int Width;
        [DataMember(Name="Height", IsRequired=true)] public int Height;
        [DataMember(Name="NeedsEnabled", IsRequired=true)] public bool NeedsEnabled;
        [DataMember(Name="BlockedTiles", IsRequired=true)] public List<PositionSaveData> BlockedTiles;
        [DataMember(Name="Colonists", IsRequired=true)] public List<ColonistSeedSaveData> Colonists;
        [DataMember(Name="Resources", IsRequired=true)] public List<ResourceSeedSaveData> Resources;
        [DataMember(Name="Beds", IsRequired=true)] public List<BedSeedSaveData> Beds;
        [DataMember(Name="Gatherables", IsRequired=true)] public List<GatherSeedSaveData> Gatherables;
    }
    [DataContract]
    public sealed class ColonistSeedSaveData
    {
        [DataMember(Name="Id", IsRequired=true)] public int Id;
        [DataMember(Name="Position", IsRequired=true)] public PositionSaveData Position;
        [DataMember(Name="Nutrition", IsRequired=true)] public int Nutrition;
        [DataMember(Name="Rest", IsRequired=true)] public int Rest;
    }
    [DataContract]
    public sealed class ResourceSeedSaveData
    {
        [DataMember(Name="Id", IsRequired=true)] public int Id;
        [DataMember(Name="Kind", IsRequired=true)] public string Kind;
        [DataMember(Name="Amount", IsRequired=true)] public int Amount;
        [DataMember(Name="Position", IsRequired=true)] public PositionSaveData Position;
    }
    [DataContract]
    public sealed class BedSeedSaveData
    {
        [DataMember(Name="Id", IsRequired=true)] public int Id;
        [DataMember(Name="Position", IsRequired=true)] public PositionSaveData Position;
    }
    [DataContract]
    public sealed class GatherSeedSaveData
    {
        [DataMember(Name="Id", IsRequired=true)] public int Id;
        [DataMember(Name="Kind", IsRequired=true)] public string Kind;
        [DataMember(Name="Position", IsRequired=true)] public PositionSaveData Position;
    }
    [DataContract]
    public sealed class WorldSaveData
    {
        [DataMember(Name="Width", IsRequired=true)] public int Width;
        [DataMember(Name="Height", IsRequired=true)] public int Height;
        [DataMember(Name="NeedsEnabled", IsRequired=true)] public bool NeedsEnabled;
        [DataMember(Name="Tick", IsRequired=true)] public long Tick;
        [DataMember(Name="MapRevision", IsRequired=true)] public long MapRevision;
        [DataMember(Name="PathSearchCount", IsRequired=true)] public long PathSearchCount;
        [DataMember(Name="LastCommandError", IsRequired=true)] public string LastCommandError;
        [DataMember(Name="NextEntityId", IsRequired=true)] public int NextEntityId;
        [DataMember(Name="RequestSequence", IsRequired=true)] public long RequestSequence;
        [DataMember(Name="AvailabilityRevision", IsRequired=true)] public long AvailabilityRevision;
        [DataMember(Name="BlockedTiles", IsRequired=true)] public List<PositionSaveData> BlockedTiles;
        [DataMember(Name="CancelledThisTick", IsRequired=true)] public List<int> CancelledThisTick;
        [DataMember(Name="MoveCommands", IsRequired=true)] public List<MoveCommandSaveData> MoveCommands;
        [DataMember(Name="Colonists", IsRequired=true)] public List<ColonistSaveData> Colonists;
        [DataMember(Name="Resources", IsRequired=true)] public List<ResourceSaveData> Resources;
        [DataMember(Name="Beds", IsRequired=true)] public List<BedSaveData> Beds;
        [DataMember(Name="Gatherables", IsRequired=true)] public List<GatherSaveData> Gatherables;
        [DataMember(Name="StorageZones", IsRequired=true)] public List<StorageZoneSaveData> StorageZones;
        [DataMember(Name="Blueprints", IsRequired=true)] public List<BlueprintSaveData> Blueprints;
        [DataMember(Name="Buildings", IsRequired=true)] public List<BuildingSaveData> Buildings;
        [DataMember(Name="GrowingZones", IsRequired=true)] public List<GrowingZoneSaveData> GrowingZones;
        [DataMember(Name="Crops", IsRequired=true)] public List<CropSaveData> Crops;
        [DataMember(Name="CookingOrders", IsRequired=true)] public List<CookingOrderSaveData> CookingOrders;
        [DataMember(Name="CookingOutputs", IsRequired=true)] public List<CookingOutputSaveData> CookingOutputs;
        [DataMember(Name="WorkPolicies", IsRequired=true)] public List<WorkPolicySaveData> WorkPolicies;
        [DataMember(Name="Produced", IsRequired=true)] public List<ResourceCountSaveData> Produced;
        [DataMember(Name="ConstructionConsumedWood", IsRequired=true)] public int ConstructionConsumedWood;
        [DataMember(Name="DemolitionReturnedWood", IsRequired=true)] public int DemolitionReturnedWood;
        [DataMember(Name="CookingConsumedIngredients", IsRequired=true)] public int CookingConsumedIngredients;
        [DataMember(Name="CookingProducedMeals", IsRequired=true)] public int CookingProducedMeals;
    }
    [DataContract]
    public sealed class MoveCommandSaveData
    {
        [DataMember(Name="Id", IsRequired=true)] public int Id;
        [DataMember(Name="Destination", IsRequired=true)] public PositionSaveData Destination;
    }
    [DataContract]
    public sealed class ColonistSaveData
    {
        [DataMember(Name="Id", IsRequired=true)] public int Id;
        [DataMember(Name="Position", IsRequired=true)] public PositionSaveData Position;
        [DataMember(Name="Destination", IsRequired=true)] public PositionSaveData Destination;
        [DataMember(Name="Path", IsRequired=true)] public List<PositionSaveData> Path;
        [DataMember(Name="MovementProgress", IsRequired=true)] public int MovementProgress;
        [DataMember(Name="MovementTicksPerTile", IsRequired=true)] public int MovementTicksPerTile;
        [DataMember(Name="Status", IsRequired=true)] public string Status;
        [DataMember(Name="HasPendingCommand", IsRequired=true)] public bool HasPendingCommand;
        [DataMember(Name="PendingDestination", IsRequired=true)] public PositionSaveData PendingDestination;
        [DataMember(Name="IsDrafted", IsRequired=true)] public bool IsDrafted;
        [DataMember(Name="ActionId", IsRequired=true)] public long ActionId;
        [DataMember(Name="HasSleepIntent", IsRequired=true)] public bool HasSleepIntent;
        [DataMember(Name="WorkActivity", IsRequired=true)] public string WorkActivity;
        [DataMember(Name="WorkTargetId", IsRequired=true)] public int? WorkTargetId;
        [DataMember(EmitDefaultValue=false)] public bool IsCookingDelivery;
        [DataMember(Name="FeatureStep", IsRequired=true)] public string FeatureStep;
        [DataMember(Name="PendingDeliveryTargetId", IsRequired=true)] public int? PendingDeliveryTargetId;
        [DataMember(Name="CargoKind", IsRequired=true)] public string CargoKind;
        [DataMember(Name="CargoAmount", IsRequired=true)] public int CargoAmount;
        [DataMember(Name="WorkReason", IsRequired=true)] public string WorkReason;
        [DataMember(Name="WorkReadyTick", IsRequired=true)] public long WorkReadyTick;
        [DataMember(Name="NextWorkSearchTick", IsRequired=true)] public long NextWorkSearchTick;
        [DataMember(Name="LastWorkRevision", IsRequired=true)] public long LastWorkRevision;
        [DataMember(Name="NextBedSearchTick", IsRequired=true)] public long NextBedSearchTick;
        [DataMember(Name="LastBedRevision", IsRequired=true)] public long LastBedRevision;
        [DataMember(Name="LastBedPosition", IsRequired=true)] public PositionSaveData LastBedPosition;
        [DataMember(Name="Nutrition", IsRequired=true)] public int Nutrition;
        [DataMember(Name="Rest", IsRequired=true)] public int Rest;
        [DataMember(Name="LifeActivity", IsRequired=true)] public string LifeActivity;
        [DataMember(Name="LifeTargetId", IsRequired=true)] public int? LifeTargetId;
        [DataMember(Name="ActionProgress", IsRequired=true)] public int ActionProgress;
        [DataMember(Name="ActionDuration", IsRequired=true)] public int ActionDuration;
        [DataMember(Name="FoodReason", IsRequired=true)] public string FoodReason;
        [DataMember(Name="SleepReason", IsRequired=true)] public string SleepReason;
        [DataMember(Name="MealsEaten", IsRequired=true)] public int MealsEaten;
        [DataMember(Name="RawMealsEaten", IsRequired=true)] public int RawMealsEaten;
        [DataMember(Name="SleepsCompleted", IsRequired=true)] public int SleepsCompleted;
        [DataMember(Name="StarvationTicks", IsRequired=true)] public long StarvationTicks;
        [DataMember(Name="NextFoodSearchTick", IsRequired=true)] public long NextFoodSearchTick;
        [DataMember(Name="LastFoodRevision", IsRequired=true)] public long LastFoodRevision;
        [DataMember(Name="LastFoodPosition", IsRequired=true)] public PositionSaveData LastFoodPosition;
    }
    [DataContract]
    public sealed class ResourceSaveData
    {
        [DataMember(Name="Id", IsRequired=true)] public int Id;
        [DataMember(Name="Kind", IsRequired=true)] public string Kind;
        [DataMember(Name="Position", IsRequired=true)] public PositionSaveData Position;
        [DataMember(Name="Amount", IsRequired=true)] public int Amount;
        [DataMember(Name="StorageSlotId", IsRequired=true)] public int? StorageSlotId;
        [DataMember(Name="CreatedTick", IsRequired=true)] public long CreatedTick;
        [DataMember(Name="Claim", IsRequired=true)] public ClaimSaveData Claim;
        [DataMember(Name="IsInUse", IsRequired=true)] public bool IsInUse;
        [DataMember(Name="Exists", IsRequired=true)] public bool Exists;
        [DataMember(Name="ConsumedAmount", IsRequired=true)] public int ConsumedAmount;
        [DataMember(Name="AddedAmount", IsRequired=true)] public int AddedAmount;
        [DataMember(Name="RemovedAmount", IsRequired=true)] public int RemovedAmount;
        [DataMember(Name="Reservations", IsRequired=true)] public List<ReservationSaveData> Reservations;
    }
    [DataContract]
    public sealed class BedSaveData
    {
        [DataMember(Name="Id", IsRequired=true)] public int Id;
        [DataMember(Name="Position", IsRequired=true)] public PositionSaveData Position;
        [DataMember(Name="Exists", IsRequired=true)] public bool Exists;
        [DataMember(Name="IsOccupied", IsRequired=true)] public bool IsOccupied;
        [DataMember(Name="Claim", IsRequired=true)] public ClaimSaveData Claim;
    }
    [DataContract]
    public sealed class GatherSaveData
    {
        [DataMember(Name="Id", IsRequired=true)] public int Id;
        [DataMember(Name="Kind", IsRequired=true)] public string Kind;
        [DataMember(Name="Position", IsRequired=true)] public PositionSaveData Position;
        [DataMember(Name="Designated", IsRequired=true)] public bool Designated;
        [DataMember(Name="Depleted", IsRequired=true)] public bool Depleted;
        [DataMember(Name="Progress", IsRequired=true)] public int Progress;
        [DataMember(Name="RequestOrder", IsRequired=true)] public long RequestOrder;
        [DataMember(Name="Claim", IsRequired=true)] public ClaimSaveData Claim;
    }
    [DataContract]
    public sealed class StorageZoneSaveData
    {
        [DataMember(Name="Id", IsRequired=true)] public int Id;
        [DataMember(Name="Exists", IsRequired=true)] public bool Exists;
        [DataMember(Name="Allowed", IsRequired=true)] public List<string> Allowed;
        [DataMember(Name="Slots", IsRequired=true)] public List<StorageSlotSaveData> Slots;
    }
    [DataContract]
    public sealed class StorageSlotSaveData
    {
        [DataMember(Name="Id", IsRequired=true)] public int Id;
        [DataMember(Name="ZoneId", IsRequired=true)] public int ZoneId;
        [DataMember(Name="Position", IsRequired=true)] public PositionSaveData Position;
        [DataMember(Name="StackId", IsRequired=true)] public int? StackId;
        [DataMember(Name="Claim", IsRequired=true)] public ClaimSaveData Claim;
    }
    [DataContract]
    public sealed class BlueprintSaveData
    {
        [DataMember(Name="Id", IsRequired=true)] public int Id;
        [DataMember(Name="Kind", IsRequired=true)] public string Kind;
        [DataMember(Name="Position", IsRequired=true)] public PositionSaveData Position;
        [DataMember(Name="RequestOrder", IsRequired=true)] public long RequestOrder;
        [DataMember(Name="Exists", IsRequired=true)] public bool Exists;
        [DataMember(Name="DeliveredWood", IsRequired=true)] public int DeliveredWood;
        [DataMember(Name="Progress", IsRequired=true)] public int Progress;
        [DataMember(Name="Reason", IsRequired=true)] public string Reason;
        [DataMember(Name="Claim", IsRequired=true)] public ClaimSaveData Claim;
    }
    [DataContract]
    public sealed class BuildingSaveData
    {
        [DataMember(Name="Id", IsRequired=true)] public int Id;
        [DataMember(Name="Kind", IsRequired=true)] public string Kind;
        [DataMember(Name="Position", IsRequired=true)] public PositionSaveData Position;
        [DataMember(Name="Exists", IsRequired=true)] public bool Exists;
        [DataMember(Name="DemolitionDesignated", IsRequired=true)] public bool DemolitionDesignated;
        [DataMember(Name="DemolitionProgress", IsRequired=true)] public int DemolitionProgress;
        [DataMember(Name="RequestOrder", IsRequired=true)] public long RequestOrder;
        [DataMember(Name="Reason", IsRequired=true)] public string Reason;
        [DataMember(Name="Claim", IsRequired=true)] public ClaimSaveData Claim;
    }
    [DataContract]
    public sealed class GrowingZoneSaveData
    {
        [DataMember(Name="Id", IsRequired=true)] public int Id;
        [DataMember(Name="Exists", IsRequired=true)] public bool Exists;
        [DataMember(Name="SowingEnabled", IsRequired=true)] public bool SowingEnabled;
        [DataMember(Name="CellIds", IsRequired=true)] public List<int> CellIds;
    }
    [DataContract]
    public sealed class CropSaveData
    {
        [DataMember(Name="Id", IsRequired=true)] public int Id;
        [DataMember(Name="ZoneId", IsRequired=true)] public int ZoneId;
        [DataMember(Name="Position", IsRequired=true)] public PositionSaveData Position;
        [DataMember(Name="Exists", IsRequired=true)] public bool Exists;
        [DataMember(Name="Stage", IsRequired=true)] public string Stage;
        [DataMember(Name="SowProgress", IsRequired=true)] public int SowProgress;
        [DataMember(Name="HarvestProgress", IsRequired=true)] public int HarvestProgress;
        [DataMember(Name="GrowthTicks", IsRequired=true)] public int GrowthTicks;
        [DataMember(Name="PlantedTick", IsRequired=true)] public long PlantedTick;
        [DataMember(Name="RequestOrder", IsRequired=true)] public long RequestOrder;
        [DataMember(Name="Reason", IsRequired=true)] public string Reason;
        [DataMember(Name="Claim", IsRequired=true)] public ClaimSaveData Claim;
    }
    [DataContract]
    public sealed class CookingOrderSaveData
    {
        [DataMember(EmitDefaultValue=false)] public int RepeatMode;
        [DataMember(EmitDefaultValue=false)] public int RemainingCount;
        [DataMember(EmitDefaultValue=false)] public int Priority;
        [DataMember(EmitDefaultValue=false)] public int? WorkerId;
        [DataMember(EmitDefaultValue=false)] public bool? AllowIngredients;
        [DataMember(EmitDefaultValue=false)] public int? IngredientRadius;
        [DataMember(EmitDefaultValue=false)] public int DeliveryMode;
        [DataMember(EmitDefaultValue=false)] public int? DestinationZoneId;
        [DataMember(Name="Id", IsRequired=true)] public int Id;
        [DataMember(Name="StoveId", IsRequired=true)] public int StoveId;
        [DataMember(Name="ServiceCell", IsRequired=true)] public PositionSaveData ServiceCell;
        [DataMember(Name="Exists", IsRequired=true)] public bool Exists;
        [DataMember(Name="Enabled", IsRequired=true)] public bool Enabled;
        [DataMember(Name="TargetMeals", IsRequired=true)] public int TargetMeals;
        [DataMember(Name="InputIngredients", IsRequired=true)] public int InputIngredients;
        [DataMember(Name="Progress", IsRequired=true)] public int Progress;
        [DataMember(Name="ActiveCycle", IsRequired=true)] public bool ActiveCycle;
        [DataMember(Name="RequestOrder", IsRequired=true)] public long RequestOrder;
        [DataMember(Name="Reason", IsRequired=true)] public string Reason;
        [DataMember(Name="CompletedMeals", IsRequired=true)] public int CompletedMeals;
        [DataMember(Name="Claim", IsRequired=true)] public ClaimSaveData Claim;
    }
    [DataContract]
    public sealed class CookingOutputSaveData
    {
        [DataMember(Name="Id", IsRequired=true)] public int Id;
        [DataMember(Name="StoveId", IsRequired=true)] public int StoveId;
        [DataMember(Name="Position", IsRequired=true)] public PositionSaveData Position;
        [DataMember(Name="Exists", IsRequired=true)] public bool Exists;
        [DataMember(Name="StackId", IsRequired=true)] public int? StackId;
        [DataMember(Name="Claim", IsRequired=true)] public ClaimSaveData Claim;
    }
    [DataContract]
    public sealed class WorkPolicySaveData
    {
        [DataMember(Name="PawnId", IsRequired=true)] public int PawnId;
        [DataMember(Name="Priorities", IsRequired=true)] public List<WorkPrioritySaveData> Priorities;
    }
    [DataContract]
    public sealed class WorkPrioritySaveData
    {
        [DataMember(Name="Category", IsRequired=true)] public string Category;
        [DataMember(Name="Priority", IsRequired=true)] public int Priority;
    }
    public sealed class SaveValidationResult
    {
        public SaveValidationResult(bool isValid,string errorCode="",string fieldPath="") { IsValid=isValid;ErrorCode=errorCode;FieldPath=fieldPath; }
        public bool IsValid { get; }
        public string ErrorCode { get; }
        public string FieldPath { get; }
    }
    public sealed class SaveCaptureResult
    {
        public SaveCaptureResult(bool success,SaveData data=null,string errorCode="") { Success=success;Data=data;ErrorCode=errorCode; }
        public bool Success { get; }
        public SaveData Data { get; }
        public string ErrorCode { get; }
    }
}
