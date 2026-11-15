using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace RimworldTweak.Simulation
{
    public static class SaveValidation
    {
        public const int MaximumFileBytes = 16 * 1024 * 1024;
        private sealed class InvalidSave : Exception { public InvalidSave(string path) { Path=path; } public string Path { get; } }
        private static void Check(bool valid,string path) { if(!valid) throw new InvalidSave(path); }
        private static bool Defined<T>(string value) where T:struct => value!=null && Enum.TryParse<T>(value,out var parsed) && Enum.IsDefined(typeof(T),parsed) && Enum.GetName(typeof(T),parsed)==value;
        private static void Position(PositionSaveData p,int width,int height,string path) => Check(p!=null&&p.X>=0&&p.Y>=0&&p.X<width&&p.Y<height,path);
        private static void List<T>(List<T> items,string path) { Check(items!=null,path);for(var i=0;i<items.Count;i++)Check(items[i]!=null,path+"["+i+"]"); }
        private static void Unique<T>(IEnumerable<T> values,string path) => Check(values.Distinct().Count()==values.Count(),path);
        private static bool Same(ClaimSaveData a,ClaimSaveData b) => a.OwnerId==b.OwnerId&&a.ActionId==b.ActionId;
        public static SaveValidationResult Validate(SaveData data)
        {
            if(data==null)return new SaveValidationResult(false,"SaveInvalidData","data");
            if(data.SchemaVersion!=1||data.ScenarioVersion!=1||data.RulesVersion!=1||data.ContractVersion!="settlement-five-v2")
                return new SaveValidationResult(false,"SaveUnsupportedVersion","header");
            try { ValidateCore(data); return new SaveValidationResult(true); }
            catch(InvalidSave e){return new SaveValidationResult(false,"SaveInvalidData",e.Path);}
            catch(OverflowException){return new SaveValidationResult(false,"SaveInvalidData","numericOverflow");}
        }
        private static void ValidateCore(SaveData d)
        {
            Check(!string.IsNullOrWhiteSpace(d.ScenarioId)&&d.ScenarioId.Length<=128,"scenarioId");
            Check(d.Session!=null,"Session");Check(d.Session.ResetCount>=0,"Session.ResetCount");
            Check(d.Session.Speed==1||d.Session.Speed==3,"Session.Speed");
            Check(decimal.TryParse(d.Session.PendingSeconds,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var remainder)&&remainder>=0m&&remainder<0.1m,"Session.PendingSeconds");
            Scenario(d.Scenario);
            var w=d.World;Check(w!=null,"World");Check(w.Width>=1&&w.Width<=64&&w.Height>=1&&w.Height<=64,"World.Size");
            Check(w.Width==d.Scenario.Width&&w.Height==d.Scenario.Height&&w.NeedsEnabled==d.Scenario.NeedsEnabled,"World.Scenario");
            Check(w.Tick>=0&&w.MapRevision>=0&&w.PathSearchCount>=0&&w.RequestSequence>=0&&w.AvailabilityRevision>=0,"World.Counters");
            Check(w.LastCommandError!=null&&w.LastCommandError.Length<=256,"World.LastCommandError");
            Check(w.ConstructionConsumedWood>=0&&w.DemolitionReturnedWood>=0&&w.CookingConsumedIngredients>=0&&w.CookingProducedMeals>=0,"World.MaterialCounters");
            List(w.BlockedTiles,"BlockedTiles");foreach(var p in w.BlockedTiles)Position(p,w.Width,w.Height,"BlockedTiles");
            Unique(w.BlockedTiles.Select(p=>p.X+":"+p.Y),"BlockedTiles.Duplicate");
            List(w.Colonists,"Colonists");Check(w.Colonists.Count>0,"Colonists.Empty");
            List(w.Resources,"Resources");List(w.Beds,"Beds");List(w.Gatherables,"Gatherables");List(w.StorageZones,"StorageZones");
            List(w.Blueprints,"Blueprints");List(w.Buildings,"Buildings");List(w.GrowingZones,"GrowingZones");List(w.Crops,"Crops");
            List(w.CookingOrders,"CookingOrders");List(w.CookingOutputs,"CookingOutputs");List(w.WorkPolicies,"WorkPolicies");
            List(w.MoveCommands,"MoveCommands");List(w.Produced,"Produced");Check(w.CancelledThisTick!=null,"CancelledThisTick");
            Unique(w.Colonists.Select(p=>p.Id),"Colonists.Id");Unique(w.Buildings.Select(b=>b.Id),"Buildings.Id");
            var pawns=w.Colonists.ToDictionary(p=>p.Id);var buildings=w.Buildings.ToDictionary(b=>b.Id);
            var ids=new HashSet<int>();var maxId=0;
            Action<int,string> add=(id,path)=>{Check(id>0&&ids.Add(id),path);maxId=Math.Max(maxId,id);};
            Action<ClaimSaveData,string> claim=(c,path)=>{
                Check(c!=null&&c.ActionId>=0,path);
                if(c.OwnerId.HasValue)Check(pawns.TryGetValue(c.OwnerId.Value,out var owner)&&owner.ActionId==c.ActionId,path+".Owner");
                else Check(c.ActionId==0,path+".EmptyAction");
            };
            foreach(var p in w.Colonists)
            {
                add(p.Id,"Colonists.Id");Position(p.Position,w.Width,w.Height,"Colonist.Position");
                if(p.Destination!=null)Position(p.Destination,w.Width,w.Height,"Colonist.Destination");
                if(p.PendingDestination!=null)Position(p.PendingDestination,w.Width,w.Height,"Colonist.PendingDestination");
                Position(p.LastBedPosition,w.Width,w.Height,"Colonist.LastBedPosition");Position(p.LastFoodPosition,w.Width,w.Height,"Colonist.LastFoodPosition");
                List(p.Path,"Colonist.Path");foreach(var point in p.Path)Position(point,w.Width,w.Height,"Colonist.Path");
                Check(Defined<MovementStatus>(p.Status)&&Defined<WorkActivity>(p.WorkActivity)&&Defined<LifeActivity>(p.LifeActivity)&&Defined<ResourceKind>(p.CargoKind),"Colonist.Enum");
                Check(p.FeatureStep==null||Defined<WorkStep>(p.FeatureStep),"Colonist.FeatureStep");
                Check(p.Nutrition>=0&&p.Nutrition<=10000&&p.Rest>=0&&p.Rest<=10000,"Colonist.Needs");
                Check(p.MovementTicksPerTile==5||p.MovementTicksPerTile==10,"Colonist.MovementTicksPerTile");
                Check(p.MovementProgress>=0&&p.MovementProgress<p.MovementTicksPerTile&&p.CargoAmount>=0&&p.CargoAmount<=10,"Colonist.ProgressCargo");
                Check(p.ActionId>=0&&p.ActionProgress>=0&&p.ActionDuration>=0&&p.MealsEaten>=0&&p.RawMealsEaten>=0&&p.SleepsCompleted>=0&&p.StarvationTicks>=0,"Colonist.Counters");
                Check(p.WorkReadyTick>=0&&p.NextWorkSearchTick>=0&&p.NextBedSearchTick>=0&&p.NextFoodSearchTick>=0&&p.LastWorkRevision>=-1&&p.LastBedRevision>=-1&&p.LastFoodRevision>=-1,"Colonist.Retry");
                Check(p.WorkReason!=null&&p.FoodReason!=null&&p.SleepReason!=null,"Colonist.Reasons");
            }
            foreach(var r in w.Resources)
            {
                add(r.Id,"Resource.Id");Position(r.Position,w.Width,w.Height,"Resource.Position");claim(r.Claim,"Resource.Claim");List(r.Reservations,"Resource.Reservations");
                Check(Defined<ResourceKind>(r.Kind)&&r.Amount>=0&&r.ConsumedAmount>=0&&r.AddedAmount>=0&&r.RemovedAmount>=0&&r.CreatedTick>=-1&&r.CreatedTick<=w.Tick,"Resource.Values");
                Check(r.Reservations.Count<=1,"Resource.Reservations.Count");
                foreach(var reservation in r.Reservations)Check(r.Claim.OwnerId==reservation.OwnerId&&reservation.Amount>0&&reservation.Amount<=r.Amount,"Resource.Reservation");
                Check(!r.IsInUse||r.Reservations.Count==1,"Resource.InUse");
            }
            foreach(var b in w.Buildings){add(b.Id,"Building.Id");Position(b.Position,w.Width,w.Height,"Building.Position");claim(b.Claim,"Building.Claim");Check(Defined<BuildingKind>(b.Kind)&&Defined<WorkReasonCode>(b.Reason)&&b.DemolitionProgress>=0&&b.DemolitionProgress<=30&&b.RequestOrder>=0&&b.RequestOrder<=w.RequestSequence,"Building.Values");}
            foreach(var b in w.Beds)
            {
                Position(b.Position,w.Width,w.Height,"Bed.Position");
                // A constructed bed deliberately shares the completed building's entity ID.
                if(buildings.TryGetValue(b.Id,out var building))Check(building.Kind==nameof(BuildingKind.Bed)&&building.Position.X==b.Position.X&&building.Position.Y==b.Position.Y,"Bed.BuildingAlias");
                else add(b.Id,"Bed.Id");
                Position(b.Position,w.Width,w.Height,"Bed.Position");claim(b.Claim,"Bed.Claim");Check(!b.IsOccupied||b.Exists&&b.Claim.OwnerId.HasValue,"Bed.Occupied");
            }
            Unique(w.Beds.Select(b=>b.Id),"Beds.Id");
            foreach(var g in w.Gatherables){add(g.Id,"Gather.Id");Position(g.Position,w.Width,w.Height,"Gather.Position");claim(g.Claim,"Gather.Claim");Check(Defined<GatherKind>(g.Kind)&&g.Progress>=0&&g.Progress<=(g.Kind==nameof(GatherKind.Tree)?60:30)&&g.RequestOrder>=0&&g.RequestOrder<=w.RequestSequence,"Gather.Values");}
            foreach(var z in w.StorageZones)
            {
                add(z.Id,"StorageZone.Id");List(z.Allowed,"StorageZone.Allowed");Check(z.Allowed.All(Defined<ResourceKind>),"StorageZone.Allowed");Unique(z.Allowed,"StorageZone.Allowed.Duplicate");List(z.Slots,"StorageZone.Slots");
                foreach(var slot in z.Slots){add(slot.Id,"Slot.Id");Check(slot.ZoneId==z.Id,"Slot.ZoneId");Position(slot.Position,w.Width,w.Height,"Slot.Position");claim(slot.Claim,"Slot.Claim");}
            }
            foreach(var b in w.Blueprints)
            {
                add(b.Id,"Blueprint.Id");Position(b.Position,w.Width,w.Height,"Blueprint.Position");claim(b.Claim,"Blueprint.Claim");
                Check(Defined<BuildingKind>(b.Kind)&&Defined<WorkReasonCode>(b.Reason),"Blueprint.Enum");var kind=(BuildingKind)Enum.Parse(typeof(BuildingKind),b.Kind);
                Check(b.DeliveredWood>=0&&b.DeliveredWood<=ConstructionRules.Cost(kind)&&b.Progress>=0&&b.Progress<=ConstructionRules.Duration(kind)&&b.RequestOrder>=0&&b.RequestOrder<=w.RequestSequence,"Blueprint.Values");
            }
            foreach(var z in w.GrowingZones){add(z.Id,"GrowingZone.Id");Check(z.CellIds!=null,"GrowingZone.Cells");Unique(z.CellIds,"GrowingZone.Cells.Duplicate");}
            foreach(var c in w.Crops){add(c.Id,"Crop.Id");Position(c.Position,w.Width,w.Height,"Crop.Position");claim(c.Claim,"Crop.Claim");Check(Defined<CropStage>(c.Stage)&&Defined<WorkReasonCode>(c.Reason)&&c.SowProgress>=0&&c.SowProgress<=20&&c.HarvestProgress>=0&&c.HarvestProgress<=30&&c.GrowthTicks>=0&&c.GrowthTicks<=2400&&c.PlantedTick>=-1&&c.PlantedTick<=w.Tick&&c.RequestOrder>=0&&c.RequestOrder<=w.RequestSequence,"Crop.Values");}
            foreach(var o in w.CookingOrders)
            {
                add(o.Id,"CookingOrder.Id");Position(o.ServiceCell,w.Width,w.Height,"CookingOrder.ServiceCell");claim(o.Claim,"CookingOrder.Claim");
                Check(o.TargetMeals>=0&&o.TargetMeals<=99&&o.InputIngredients>=0&&o.InputIngredients<=2&&o.Progress>=0&&o.Progress<40&&o.CompletedMeals>=0&&o.RequestOrder>=0&&o.RequestOrder<=w.RequestSequence&&Defined<WorkReasonCode>(o.Reason),"CookingOrder.Values");
                Check(buildings.TryGetValue(o.StoveId,out var b)&&b.Kind==nameof(BuildingKind.Stove),"CookingOrder.StoveId");
                Check(o.ServiceCell.X==b.Position.X&&o.ServiceCell.Y==b.Position.Y-1,"CookingOrder.ServiceCell");
                // Deleted order records still reference the same physical building claim.
                Check(Same(o.Claim,b.Claim),"CookingOrder.SharedClaim");
            }
            Unique(w.CookingOrders.Where(o=>o.Exists).Select(o=>o.StoveId),"CookingOrder.StoveDuplicate");
            foreach(var o in w.CookingOutputs){add(o.Id,"CookingOutput.Id");Position(o.Position,w.Width,w.Height,"CookingOutput.Position");claim(o.Claim,"CookingOutput.Claim");Check(buildings.TryGetValue(o.StoveId,out var b)&&b.Kind==nameof(BuildingKind.Stove)&&o.Position.X==b.Position.X&&o.Position.Y==b.Position.Y-1,"CookingOutput.StoveId");}
            Unique(w.CookingOutputs.Where(o=>o.Exists).Select(o=>o.StoveId),"CookingOutput.StoveDuplicate");
            Check(w.NextEntityId>maxId,"World.NextEntityId");
            var resources=w.Resources.ToDictionary(r=>r.Id);var slots=w.StorageZones.SelectMany(z=>z.Slots).ToDictionary(s=>s.Id);var crops=w.Crops.ToDictionary(c=>c.Id);
            var stackLinks=new HashSet<int>();
            foreach(var z in w.StorageZones)foreach(var slot in z.Slots)if(slot.StackId.HasValue)
            {
                Check(resources.TryGetValue(slot.StackId.Value,out var r)&&stackLinks.Add(r.Id),"Slot.StackId");
                Check(r.StorageSlotId==slot.Id&&Same(r.Claim,slot.Claim)&&r.Position.X==slot.Position.X&&r.Position.Y==slot.Position.Y&&r.Amount<=50,"Slot.SharedStack");
            }
            foreach(var r in w.Resources)if(r.StorageSlotId.HasValue)Check(slots.TryGetValue(r.StorageSlotId.Value,out var s)&&s.StackId==r.Id,"Resource.StorageSlotId");
            foreach(var o in w.CookingOutputs)if(o.StackId.HasValue)
            {
                Check(resources.TryGetValue(o.StackId.Value,out var r),"CookingOutput.StackId");
                if(o.Exists)Check(stackLinks.Add(r.Id)&&r.Kind==nameof(ResourceKind.Meal)&&r.StorageSlotId==null&&Same(r.Claim,o.Claim)&&r.Amount<=50&&r.Position.X==o.Position.X&&r.Position.Y==o.Position.Y,"CookingOutput.SharedStack");
            }
            foreach(var z in w.GrowingZones)foreach(var id in z.CellIds)Check(crops.TryGetValue(id,out var c)&&c.ZoneId==z.Id,"GrowingZone.CellId");
            foreach(var c in w.Crops)Check(w.GrowingZones.Any(z=>z.Id==c.ZoneId&&z.CellIds.Contains(c.Id)),"Crop.ZoneId");
            foreach(var p in w.Colonists){if(p.WorkTargetId.HasValue)Check(ids.Contains(p.WorkTargetId.Value),"Colonist.WorkTargetId");if(p.LifeTargetId.HasValue)Check(ids.Contains(p.LifeTargetId.Value),"Colonist.LifeTargetId");if(p.PendingDeliveryTargetId.HasValue)Check(ids.Contains(p.PendingDeliveryTargetId.Value),"Colonist.PendingDeliveryTargetId");}
            foreach(var m in w.MoveCommands){Check(pawns.ContainsKey(m.Id),"MoveCommand.Id");if(m.Destination!=null)Position(m.Destination,w.Width,w.Height,"MoveCommand.Destination");}
            foreach(var id in w.CancelledThisTick)Check(pawns.ContainsKey(id),"CancelledThisTick.Id");Unique(w.CancelledThisTick,"CancelledThisTick.Duplicate");
            Check(w.WorkPolicies.Count==w.Colonists.Count,"WorkPolicies.Count");Unique(w.WorkPolicies.Select(p=>p.PawnId),"WorkPolicies.PawnId");
            foreach(var p in w.WorkPolicies){Check(pawns.ContainsKey(p.PawnId),"WorkPolicy.PawnId");List(p.Priorities,"WorkPolicy.Priorities");Check(p.Priorities.Count==5,"WorkPolicy.Priorities.Count");Unique(p.Priorities.Select(v=>v.Category),"WorkPolicy.Category");foreach(var v in p.Priorities)Check(Defined<WorkCategory>(v.Category)&&v.Priority>=0&&v.Priority<=3,"WorkPolicy.Priority");}
            Unique(w.Produced.Select(p=>p.Kind),"Produced.Kind");foreach(var p in w.Produced)Check(Defined<ResourceKind>(p.Kind)&&p.Amount>=0,"Produced.Values");
            ValidateMaterialLedger(d);
        }
        private static void ValidateMaterialLedger(SaveData data)
        {
            var w=data.World;
            foreach(ResourceKind kind in Enum.GetValues(typeof(ResourceKind)))
            {
                var name=kind.ToString();long expected=0;long actual=0;
                checked
                {
                    foreach(var r in data.Scenario.Resources)if(r.Kind==name)expected+=r.Amount;
                    foreach(var p in w.Produced)if(p.Kind==name)expected+=p.Amount;
                    foreach(var r in w.Resources)if(r.Kind==name)
                    { expected+=(long)r.AddedAmount-r.RemovedAmount-r.ConsumedAmount;if(r.Exists)actual+=r.Amount; }
                    foreach(var pawn in w.Colonists)if(pawn.CargoKind==name)actual+=pawn.CargoAmount;
                    if(kind==ResourceKind.Wood)
                    { expected+=(long)w.DemolitionReturnedWood-w.ConstructionConsumedWood;foreach(var b in w.Blueprints)if(b.Exists)actual+=b.DeliveredWood; }
                    if(kind==ResourceKind.Ingredients)
                    { expected-=w.CookingConsumedIngredients;foreach(var o in w.CookingOrders)if(o.Exists)actual+=o.InputIngredients; }
                }
                // CookingProducedMeals is already included in Produced(Meal); never count it twice.
                Check(expected>=0&&actual==expected,"World.MaterialLedger."+name);
            }
        }
        private static void Scenario(ScenarioSaveData s)
        {
            Check(s!=null,"Scenario");Check(s.Width>=1&&s.Width<=64&&s.Height>=1&&s.Height<=64,"Scenario.Size");
            List(s.BlockedTiles,"Scenario.BlockedTiles");foreach(var p in s.BlockedTiles)Position(p,s.Width,s.Height,"Scenario.BlockedTiles");
            Unique(s.BlockedTiles.Select(p=>p.X+":"+p.Y),"Scenario.BlockedTiles.Duplicate");
            List(s.Colonists,"Scenario.Colonists");List(s.Resources,"Scenario.Resources");List(s.Beds,"Scenario.Beds");List(s.Gatherables,"Scenario.Gatherables");Check(s.Colonists.Count>0,"Scenario.Colonists.Empty");
            var ids=new HashSet<int>();Action<int> id=n=>Check(n>0&&ids.Add(n),"Scenario.DuplicateId");
            foreach(var p in s.Colonists){id(p.Id);Position(p.Position,s.Width,s.Height,"Scenario.Colonist.Position");Check(p.Nutrition>=0&&p.Nutrition<=10000&&p.Rest>=0&&p.Rest<=10000,"Scenario.Colonist.Needs");}
            foreach(var r in s.Resources){id(r.Id);Position(r.Position,s.Width,s.Height,"Scenario.Resource.Position");Check(Defined<ResourceKind>(r.Kind)&&r.Amount>0,"Scenario.Resource.Values");}
            foreach(var b in s.Beds){id(b.Id);Position(b.Position,s.Width,s.Height,"Scenario.Bed.Position");}
            foreach(var g in s.Gatherables){id(g.Id);Position(g.Position,s.Width,s.Height,"Scenario.Gather.Position");Check(Defined<GatherKind>(g.Kind),"Scenario.Gather.Kind");}
        }
    }
}
