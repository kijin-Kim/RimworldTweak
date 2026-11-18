using System;
using System.Linq;
using System.Collections.Generic;
namespace RimworldTweak.Simulation
{
    public sealed partial class WorldState
    {
        internal bool CanCaptureSave => !advancingTick&&topologyCommands.Count==0&&lifeCommands.Count==0;
        internal WorldSaveData CaptureSave()
        {
            return new WorldSaveData {
                Width=Width,Height=Height,NeedsEnabled=NeedsEnabled,Tick=Tick,MapRevision=MapRevision,PathSearchCount=PathSearchCount,LastCommandError=LastCommandError,
                NextEntityId=checked(nextEntityId+1),RequestSequence=requestSequence,AvailabilityRevision=availabilityRevision,
                BlockedTiles=blockedTiles.Select(PositionSaveData.From).ToList(),CancelledThisTick=cancelledThisTick.OrderBy(x=>x).ToList(),
                MoveCommands=moveCommands.Select(x=>new MoveCommandSaveData{Id=x.Id,Destination=x.Destination.HasValue?PositionSaveData.From(x.Destination.Value):null}).ToList(),
                Colonists=Colonists.Select(x=>x.CaptureSave()).ToList(),Resources=Resources.Select(x=>x.CaptureSave()).ToList(),Beds=Beds.Select(x=>x.CaptureSave()).ToList(),
                Gatherables=gatherables.Select(x=>x.CaptureSave()).ToList(),StorageZones=storageZones.Select(x=>x.CaptureSave()).ToList(),
                Blueprints=blueprints.Select(x=>x.CaptureSave()).ToList(),Buildings=buildings.Select(x=>x.CaptureSave()).ToList(),
                GrowingZones=growingZones.Select(x=>x.CaptureSave()).ToList(),Crops=crops.Select(x=>x.CaptureSave()).ToList(),
                CookingOrders=cookingOrders.Select(x=>x.CaptureSave()).ToList(),CookingOutputs=cookingOutputs.Select(x=>x.CaptureSave()).ToList(),
                WorkPolicies=workPolicies.Select(x=>new WorkPolicySaveData{PawnId=x.PawnId,Priorities=((WorkCategory[])Enum.GetValues(typeof(WorkCategory))).Select(c=>new WorkPrioritySaveData{Category=c.ToString(),Priority=x.GetPriority(c)}).ToList()}).ToList(),
                Produced=produced.OrderBy(x=>x.Key).Select(x=>new ResourceCountSaveData{Kind=x.Key.ToString(),Amount=x.Value}).ToList(),
                ConstructionConsumedWood=ConstructionConsumedWood,DemolitionReturnedWood=DemolitionReturnedWood,CookingConsumedIngredients=CookingConsumedIngredients,CookingProducedMeals=CookingProducedMeals
            };
        }
        internal static WorldState RestoreSave(WorldSaveData d)
        {
            var pawns=d.Colonists.Select(ColonistState.RestoreSave).OrderBy(x=>x.Id).ToList();
            var resources=d.Resources.Select(ResourceState.RestoreSave).ToList();
            var beds=d.Beds.Select(BedState.RestoreSave).ToList();
            var w=new WorldState(d.Width,d.Height,d.BlockedTiles.Select(x=>x.ToPosition()).ToList(),pawns,resources,beds,d.NeedsEnabled) {
                Tick=d.Tick,MapRevision=d.MapRevision,PathSearchCount=d.PathSearchCount,LastCommandError=d.LastCommandError,
                nextEntityId=d.NextEntityId-1,requestSequence=d.RequestSequence,availabilityRevision=d.AvailabilityRevision,
                ConstructionConsumedWood=d.ConstructionConsumedWood,DemolitionReturnedWood=d.DemolitionReturnedWood,CookingConsumedIngredients=d.CookingConsumedIngredients,CookingProducedMeals=d.CookingProducedMeals
            };
            foreach(var id in d.CancelledThisTick)w.cancelledThisTick.Add(id);
            foreach(var command in d.MoveCommands)w.moveCommands.Add(new MoveCommand(command.Id,command.Destination?.ToPosition()));
            foreach(var x in d.Produced)w.produced.Add(SaveState.Enum<ResourceKind>(x.Kind),x.Amount);
            foreach(var x in d.Gatherables)w.gatherables.Add(GatherState.RestoreSave(x));
            foreach(var x in d.StorageZones)
            {
                var slots=x.Slots.Select(StorageSlotState.RestoreSave).ToList();
                for(var i=0;i<slots.Count;i++)if(x.Slots[i].StackId.HasValue)
                {
                    var stack=w.resourcesById[x.Slots[i].StackId.Value];slots[i].Stack=stack;
                    if(x.Exists&&stack.StorageSlotId==slots[i].Id)stack.AttachClaim(slots[i].Claim);
                }
                w.storageZones.Add(StorageZoneState.RestoreSave(x,slots));
            }
            foreach(var x in d.Blueprints)w.blueprints.Add(BlueprintState.RestoreSave(x));
            foreach(var x in d.Buildings)w.buildings.Add(BuildingState.RestoreSave(x));
            foreach(var x in d.Crops)w.crops.Add(CropState.RestoreSave(x));
            foreach(var x in d.GrowingZones)w.growingZones.Add(GrowingZoneState.RestoreSave(x,x.CellIds.Select(id=>w.crops.Single(c=>c.Id==id)).ToList()));
            foreach(var x in d.CookingOrders)w.cookingOrders.Add(CookingOrderState.RestoreSave(x,w.buildings.Single(b=>b.Id==x.StoveId).Claim));
            foreach(var x in d.CookingOutputs)
            {
                var output=CookingOutputState.RestoreSave(x);
                if(x.StackId.HasValue){output.Stack=w.resourcesById[x.StackId.Value];if(x.Exists)output.Stack.AttachClaim(output.Claim);}
                w.cookingOutputs.Add(output);
            }
            foreach(var x in d.WorkPolicies)foreach(var p in x.Priorities)w.PolicyFor(x.PawnId).SetPriority(SaveState.Enum<WorkCategory>(p.Category),p.Priority);
            w.ValidateRestoredActions();
            return w;
        }
        internal void ValidateSaveLedger(ScenarioDefinition original)
        {
            checked
            {
                foreach(ResourceKind kind in Enum.GetValues(typeof(ResourceKind)))
                {
                    long expected=original.Resources.Where(x=>x.Kind==kind).Sum(x=>(long)x.Amount)+ProducedAmount(kind);
                    foreach(var resource in Resources)if(resource.Kind==kind)expected+=(long)resource.AddedAmount-resource.RemovedAmount-resource.ConsumedAmount;
                    if(kind==ResourceKind.Wood)expected+=(long)DemolitionReturnedWood-ConstructionConsumedWood;
                    if(kind==ResourceKind.Ingredients)expected-=CookingConsumedIngredients;
                    long actual=Resources.Where(x=>x.Exists&&x.Kind==kind).Sum(x=>(long)x.Amount)+Colonists.Where(x=>x.CargoKind==kind).Sum(x=>(long)x.CargoAmount);
                    if(kind==ResourceKind.Wood)actual+=blueprints.Where(x=>x.Exists).Sum(x=>(long)x.DeliveredWood);
                    if(kind==ResourceKind.Ingredients)actual+=cookingOrders.Where(x=>x.Exists).Sum(x=>(long)x.InputIngredients);
                    RequireSave(expected==actual);
                }
                RequireSave(CookingProducedMeals==ProducedAmount(ResourceKind.Meal));
                RequireSave((long)CookingConsumedIngredients==2L*CookingProducedMeals);
                RequireSave(cookingOrders.Sum(x=>(long)x.CompletedMeals)==CookingProducedMeals);
            }
        }
        private void ValidateSaveStructure()
        {
            foreach(var b in buildings)
            {
                if(b.Exists&&ConstructionRules.BlocksMovement(b.Kind))RequireSave(blockedSet.Contains(b.Position));
                if(b.Kind==BuildingKind.Bed)
                    RequireSave(bedsById.TryGetValue(b.Id,out var bed)&&bed.Exists==(b.Exists&&!b.DemolitionDesignated));
                if(b.Exists&&b.Kind==BuildingKind.Stove)RequireSave(CookingOutput(b.Id)!=null);
            }
            foreach(var b in blueprints)if(!b.Exists)RequireSave(b.DeliveredWood==0&&!b.OwnerId.HasValue);
            foreach(var c in crops)
            {
                var z=growingZones.Single(x=>x.Id==c.ZoneId);RequireSave(c.Exists==z.Exists);
                if(c.Stage==CropStage.Growing)RequireSave(c.SowProgress==20&&c.GrowthTicks<2400&&c.PlantedTick>=0);
                if(c.Stage==CropStage.Mature)RequireSave(c.SowProgress==20&&c.GrowthTicks==2400);
            }
            foreach(var o in cookingOrders)
            {
                if(!o.Exists)RequireSave(o.InputIngredients==0&&o.Progress==0&&!o.ActiveCycle);
                else {RequireSave(Building(o.StoveId)!=null);if(o.Progress>0)RequireSave(o.InputIngredients==2);}
            }
        }
        private void ValidatePawnStage(ColonistState pawn)
        {
            var delivery=pawn.FeatureStep==WorkStep.DeliverBuild||pawn.FeatureStep==WorkStep.DeliverCook;
            var delivering=pawn.WorkActivity==WorkActivity.GoingToBuildDelivery||pawn.WorkActivity==WorkActivity.DeliveringBuild;
            var picking=pawn.WorkActivity==WorkActivity.GoingToBuildPickup||pawn.WorkActivity==WorkActivity.PickingUpBuild;
            var store=pawn.WorkActivity==WorkActivity.GoingToStore||pawn.WorkActivity==WorkActivity.Storing;
            RequireSave((pawn.CargoAmount>0)==(store||delivery&&delivering));
            if(pawn.FeatureStep.HasValue)
            {
                RequireSave(pawn.IsWorking);
                if(delivery)
                {RequireSave((delivering||picking)&&pawn.PendingDeliveryTargetId.HasValue);if(delivering)RequireSave(pawn.CargoKind==(pawn.FeatureStep==WorkStep.DeliverBuild?ResourceKind.Wood:ResourceKind.Ingredients));}
                else RequireSave((pawn.FeatureStep==WorkStep.Construct||pawn.FeatureStep==WorkStep.Demolish||pawn.FeatureStep==WorkStep.Sow||pawn.FeatureStep==WorkStep.Harvest||pawn.FeatureStep==WorkStep.Cook)&&
                    (pawn.WorkActivity==WorkActivity.GoingToFeature||pawn.WorkActivity==WorkActivity.WorkingFeature)&&!pawn.PendingDeliveryTargetId.HasValue);
            }
            else RequireSave(!picking&&!delivering&&pawn.WorkActivity!=WorkActivity.GoingToFeature&&pawn.WorkActivity!=WorkActivity.WorkingFeature&&!pawn.PendingDeliveryTargetId.HasValue);
            if(picking||pawn.WorkActivity==WorkActivity.GoingToPickup||pawn.WorkActivity==WorkActivity.PickingUp)
            {
                RequireSave(pawn.WorkTargetId.HasValue&&resourcesById.TryGetValue(pawn.WorkTargetId.Value,out var source));
                var r=resourcesById[pawn.WorkTargetId.Value];
                RequireSave(r.Reservations.Count==1&&r.Reservations.TryGetValue(pawn.Id,out var reserved)&&reserved>0&&reserved<=10&&!r.IsInUse);
                if(delivery)RequireSave(r.Kind==(pawn.FeatureStep==WorkStep.DeliverBuild?ResourceKind.Wood:ResourceKind.Ingredients));
            }
            if(pawn.IsCookingDelivery)RequireSave(!pawn.FeatureStep.HasValue&&pawn.CargoKind==ResourceKind.Meal&&pawn.CargoAmount==1&&
                (pawn.WorkActivity==WorkActivity.GoingToStore||pawn.WorkActivity==WorkActivity.Storing));
            if(!pawn.IsWorking)RequireSave(!pawn.WorkTargetId.HasValue);
            if(!pawn.IsLifeBusy)RequireSave(!pawn.LifeTargetId.HasValue&&pawn.ActionDuration==0&&pawn.ActionProgress==0);
            if(pawn.LifeActivity==LifeActivity.GoingToFood||pawn.LifeActivity==LifeActivity.Eating)
            {
                RequireSave(pawn.LifeTargetId.HasValue&&resourcesById.TryGetValue(pawn.LifeTargetId.Value,out var resource));
                var food=resourcesById[pawn.LifeTargetId.Value];RequireSave(food.Kind!=ResourceKind.Wood);RequireSave(pawn.ActionDuration==FoodDuration(food)&&pawn.ActionProgress<pawn.ActionDuration);
                if(pawn.LifeActivity==LifeActivity.Eating)RequireSave(pawn.Position==food.Position);
                else RequireSave(pawn.ActionProgress==0);
            }
            else RequireSave(pawn.ActionDuration==0&&(pawn.IsSleeping||pawn.ActionProgress==0));
            if(pawn.LifeActivity==LifeActivity.SleepingOnFloor)RequireSave(!pawn.LifeTargetId.HasValue&&!pawn.Destination.HasValue);
            if(pawn.LifeActivity==LifeActivity.SleepingInBed)RequireSave(pawn.LifeTargetId.HasValue&&bedsById.TryGetValue(pawn.LifeTargetId.Value,out var bed)&&pawn.Position==bed.Position);
            if(pawn.WorkActivity==WorkActivity.WorkingFeature)
            {
                var id=pawn.WorkTargetId??-1;
                if(pawn.FeatureStep==WorkStep.Cook)RequireSave(CookingOrder(id)!=null&&pawn.Position==CookingOrder(id).ServiceCell);
                else if(pawn.FeatureStep==WorkStep.Sow||pawn.FeatureStep==WorkStep.Harvest)RequireSave(Crop(id)!=null&&pawn.Position==Crop(id).Position);
                else {var site=pawn.FeatureStep==WorkStep.Construct?Blueprint(id)?.Position:Building(id)?.Position;RequireSave(site.HasValue&&Math.Abs(site.Value.X-pawn.Position.X)+Math.Abs(site.Value.Y-pawn.Position.Y)==1);}
            }
            if(pawn.Status!=MovementStatus.Moving)RequireSave(pawn.Path.Count==0&&pawn.MovementProgress==0);
            if(pawn.Status==MovementStatus.Idle)RequireSave(!pawn.Destination.HasValue);
            if(pawn.Status==MovementStatus.Arrived)RequireSave(pawn.Destination.HasValue&&pawn.Position==pawn.Destination.Value);
        }
        private static void RequireSave(bool condition) {if(!condition)throw new ArgumentException("SaveInvalidData");}
        // Validate authorities once per physical claim, allowing slot/stack and stove/order aliases.
        private void ValidateRestoredActions()
        {
            ValidateSaveStructure();
            var claims=new Dictionary<InteractionClaim,int>();
            foreach(var r in Resources)claims[r.Claim]=r.Id;
            foreach(var z in storageZones)foreach(var s in z.Slots)if(z.Exists)claims[s.Claim]=s.Id;
            foreach(var b in blueprints)claims[b.Claim]=b.Id;
            foreach(var b in buildings)claims[b.Claim]=b.Id;
            foreach(var c in crops)claims[c.Claim]=c.Id;
            foreach(var o in cookingOutputs)if(o.Exists)claims[o.Claim]=o.Stack?.Id??o.Id;
            foreach(var n in gatherables)claims[n.Claim]=n.Id;
            var held=new Dictionary<int,int>();
            foreach(var entry in claims)if(entry.Key.OwnerId.HasValue)
            {
                RequireSave(colonistsById.TryGetValue(entry.Key.OwnerId.Value,out var pawn));
                RequireSave(pawn.ActionId==entry.Key.ActionId&&!pawn.IsDrafted);
                held[pawn.Id]=held.TryGetValue(pawn.Id,out var count)?count+1:1;
                var related=pawn.WorkTargetId==entry.Value||pawn.LifeTargetId==entry.Value;
                if(!related&&resourcesById.TryGetValue(pawn.WorkTargetId??pawn.LifeTargetId??-1,out var resource))related=ReferenceEquals(resource.Claim,entry.Key);
                RequireSave(related);
            }
            foreach(var bed in Beds)if(bed.OwnerId.HasValue)
            {
                RequireSave(colonistsById.TryGetValue(bed.OwnerId.Value,out var pawn)&&bed.OwnerActionId==pawn.ActionId&&pawn.LifeTargetId==bed.Id&&!pawn.IsDrafted);
                held[pawn.Id]=held.TryGetValue(pawn.Id,out var count)?count+1:1;
                RequireSave(bed.IsOccupied==(pawn.LifeActivity==LifeActivity.SleepingInBed));
            }
            foreach(var pawn in Colonists)
            {
                ValidatePawnStage(pawn);
                RequireSave(!held.TryGetValue(pawn.Id,out var count)||count==1);
                RequireSave(!pawn.IsDrafted||!pawn.IsWorking&&!pawn.IsLifeBusy);
                RequireSave(!(pawn.IsWorking&&pawn.IsLifeBusy));
                if(pawn.IsWorking)
                {
                    RequireSave(held.ContainsKey(pawn.Id)&&pawn.WorkTargetId.HasValue&&PolicyFor(pawn.Id).GetPriority(CurrentWorkCategory(pawn))>0);
                    if(pawn.FeatureStep.HasValue)RequireSave(ValidProductionWork(pawn));
                    else if(pawn.WorkActivity==WorkActivity.GoingToGather||pawn.WorkActivity==WorkActivity.Gathering)
                    {var node=Gather(pawn.WorkTargetId.Value);RequireSave(node!=null&&!node.Depleted&&node.Designated&&node.Claim.Matches(pawn.Id,pawn.ActionId));}
                    else if(pawn.WorkActivity==WorkActivity.GoingToPickup||pawn.WorkActivity==WorkActivity.PickingUp)
                    {RequireSave(resourcesById.TryGetValue(pawn.WorkTargetId.Value,out var resource)&&resource.Exists&&resource.Amount>0&&resource.Claim.Matches(pawn.Id,pawn.ActionId));}
                    else
                    {var slot=Slot(pawn.WorkTargetId.Value);RequireSave(slot!=null&&slot.Claim.Matches(pawn.Id,pawn.ActionId)&&pawn.CargoAmount>0&&Zone(slot.ZoneId).Allows(pawn.CargoKind)&&slot.Amount+pawn.CargoAmount<=50);}
                }
                if(pawn.LifeActivity==LifeActivity.GoingToFood||pawn.LifeActivity==LifeActivity.Eating)
                {
                    RequireSave(pawn.LifeTargetId.HasValue&&resourcesById.TryGetValue(pawn.LifeTargetId.Value,out var food));
                    var resource=resourcesById[pawn.LifeTargetId.Value];RequireSave(resource.HasReservation(pawn.Id,pawn.ActionId,FoodAmount(resource)));
                    RequireSave(resource.IsInUse==(pawn.LifeActivity==LifeActivity.Eating));
                }
                if(pawn.LifeActivity==LifeActivity.GoingToBed||pawn.LifeActivity==LifeActivity.SleepingInBed)
                    RequireSave(pawn.LifeTargetId.HasValue&&bedsById.TryGetValue(pawn.LifeTargetId.Value,out var bed)&&bed.Exists&&bed.OwnerId==pawn.Id&&bed.OwnerActionId==pawn.ActionId);
                if(!pawn.IsWorking&&!pawn.IsLifeBusy)RequireSave(!held.ContainsKey(pawn.Id));
                var pending=moveCommands.Where(c=>c.Id==pawn.Id).ToList();
                RequireSave(pawn.HasPendingCommand==(pending.Count>0));
                if(pending.Count>0)RequireSave(pawn.IsDrafted&&pawn.PendingDestination==pending[pending.Count-1].Destination);
                if(pawn.Status==MovementStatus.Moving)
                {
                    RequireSave(pawn.Destination.HasValue&&pawn.Path.Count>0);
                    var position=pawn.Position;var valid=true;
                    foreach(var cell in pawn.Path){if(Math.Abs(cell.X-position.X)+Math.Abs(cell.Y-position.Y)!=1||!InBounds(cell)||blockedSet.Contains(cell))valid=false;position=cell;}
                    valid &= position==pawn.Destination.Value;
                    var route=ReadPath(pawn.Position,pawn.Destination.Value);
                    if(!valid||route==null||route.Count==0||route[0]!=pawn.Path[0])
                    {
                        // Only this detached candidate is changed. A valid saved next cell retains sub-tile progress.
                        if(pawn.IsWorking||pawn.IsLifeBusy){EndLife(pawn);pawn.WorkReason="RestoreInvalid";}
                        else pawn.SetRoute(pawn.Destination.Value,route);
                    }
                }
            }
        }
    }
}
