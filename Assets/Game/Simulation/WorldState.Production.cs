using System;
using System.Collections.Generic;

namespace RimworldTweak.Simulation
{
    public sealed partial class WorldState
    {
        private readonly List<BlueprintState> blueprints = new List<BlueprintState>();
        private readonly List<BuildingState> buildings = new List<BuildingState>();
        private readonly List<GrowingZoneState> growingZones = new List<GrowingZoneState>();
        private readonly List<CropState> crops = new List<CropState>();
        private List<BedState> mutableBeds;
        public IReadOnlyList<BlueprintState> Blueprints => blueprints.AsReadOnly();
        public IReadOnlyList<BuildingState> Buildings => buildings.AsReadOnly();
        public IReadOnlyList<GrowingZoneState> GrowingZones => growingZones.AsReadOnly();
        public IReadOnlyList<CropState> Crops => crops.AsReadOnly();
        public int ConstructionConsumedWood { get; private set; }
        public int DemolitionReturnedWood { get; private set; }
        public int ConstructionWood => SumConstructionWood();
        private int SumConstructionWood() { var n=0; foreach(var b in blueprints) if(b.Exists) n+=b.DeliveredWood; return n; }
        private BlueprintState Blueprint(int id) => blueprints.Find(b=>b.Id==id && b.Exists);
        private BuildingState Building(int id) => buildings.Find(b=>b.Id==id && b.Exists);
        private CropState Crop(int id) => crops.Find(c=>c.Id==id && c.Exists);
        private GrowingZoneState GrowingZone(int id) => growingZones.Find(z=>z.Id==id && z.Exists);
        private bool CanUseArea(GridPosition first, GridPosition last, bool allowResources)
        {
            if (!InBounds(first) || !InBounds(last)) return false;
            var left = Math.Min(first.X, last.X);
            var right = Math.Max(first.X, last.X);
            var bottom = Math.Min(first.Y, last.Y);
            var top = Math.Max(first.Y, last.Y);
            bool Contains(GridPosition p) => p.X >= left && p.X <= right && p.Y >= bottom && p.Y <= top;
            bool ContainsBuilding(BuildingKind kind, GridPosition p) => Contains(p) ||
                (kind == BuildingKind.Stove && Contains(ConstructionRules.ServiceCell(p)));

            // Inspect the occupied cells once, including for a map-spanning drag.
            foreach (var p in blockedTiles) if (Contains(p)) return false;
            foreach (var b in blueprints) if (b.Exists && ContainsBuilding(b.Kind, b.Position)) return false;
            foreach (var b in buildings) if (b.Exists && ContainsBuilding(b.Kind, b.Position)) return false;
            foreach (var c in crops) if (c.Exists && Contains(c.Position)) return false;
            foreach (var bed in Beds) if (bed.Exists && Contains(bed.Position)) return false;
            foreach (var node in gatherables) if (!node.Depleted && Contains(node.Position)) return false;
            foreach (var zone in storageZones)
                if (zone.Exists)
                    foreach (var slot in zone.Slots) if (Contains(slot.Position)) return false;
            if (!allowResources)
                foreach (var r in Resources) if (r.Exists && r.Amount > 0 && Contains(r.Position)) return false;
            return true;
        }

        private bool CanPlaceProduction(GridPosition p) => CanUseArea(p, p, false);
        public bool PlaceBlueprint(BuildingKind kind, GridPosition position)
        {
            if(!CanPlaceBlueprint(kind, position, out var reason)) return Reject(reason);
            blueprints.Add(new BlueprintState(NewId(),kind,position,++requestSequence)); availabilityRevision++;
            LastCommandError=string.Empty; return true;
        }
        // Preview and commit share the same read-only rules. A preview never changes LastCommandError or IDs.
        public bool CanPlaceBlueprint(BuildingKind kind, GridPosition position, out string reason)
        {
            reason = !Enum.IsDefined(typeof(BuildingKind),kind) || !CanPlaceProduction(position) ? "InvalidBlueprint"
                : kind==BuildingKind.Stove && !CanPlaceProduction(ConstructionRules.ServiceCell(position)) ? "OccupiedServiceCell" : string.Empty;
            return reason.Length==0;
        }
        public bool CanCreateGrowingZone(GridPosition first,GridPosition second) => CanUseArea(first, second, false);
        private void CancelProductionOwners(int targetId)
        {
            foreach(var pawn in Colonists)
                if(pawn.WorkTargetId==targetId || pawn.PendingDeliveryTargetId==targetId || pawn.LifeTargetId==targetId)
                { EndLife(pawn); cancelledThisTick.Add(pawn.Id); }
        }
        public bool CancelBlueprint(int blueprintId)
        {
            var b=Blueprint(blueprintId); if(b==null) return Reject("InvalidBlueprint");
            CancelProductionOwners(b.Id);
            if(b.DeliveredWood>0) SpawnResource(ResourceKind.Wood,b.DeliveredWood,b.Position);
            b.DeliveredWood=0; b.Exists=false; availabilityRevision++; LastCommandError=string.Empty; return true;
        }
        public bool SetDemolition(int buildingId,bool enabled)
        {
            var b=Building(buildingId); if(b==null) return Reject("InvalidBuilding");
            if(b.DemolitionDesignated!=enabled)
            {
                CancelProductionOwners(b.Id); b.DemolitionDesignated=enabled;
                if(enabled) b.RequestOrder=++requestSequence;
                if(b.Kind==BuildingKind.Stove) ReconcileCookingCycles(false);
                if(b.Kind==BuildingKind.Bed) bedsById[b.Id].SetExists(!enabled);
                availabilityRevision++;
            }
            LastCommandError=string.Empty; return true;
        }
        public bool CreateGrowingZone(GridPosition first,GridPosition second)
        {
            if(!CanCreateGrowingZone(first,second)) return Reject("InvalidGrowingArea");
            var id=NewId(); var cells=new List<CropState>();
            for(var y=Math.Min(first.Y,second.Y);y<=Math.Max(first.Y,second.Y);y++)
                for(var x=Math.Min(first.X,second.X);x<=Math.Max(first.X,second.X);x++)
                { var c=new CropState(NewId(),id,new GridPosition(x,y),++requestSequence); cells.Add(c); crops.Add(c); }
            growingZones.Add(new GrowingZoneState(id,cells)); availabilityRevision++; LastCommandError=string.Empty; return true;
        }
        public bool SetSowingEnabled(int zoneId,bool enabled)
        {
            var zone=GrowingZone(zoneId); if(zone==null) return Reject("InvalidGrowingZone");
            if(zone.SowingEnabled!=enabled)
            {
                zone.SowingEnabled=enabled;
                foreach(var c in zone.Cells) if(c.Stage==CropStage.Empty)
                { if(!enabled) CancelProductionOwners(c.Id); c.Reason=enabled?WorkReasonCode.None:WorkReasonCode.Disabled; }
                availabilityRevision++;
            }
            LastCommandError=string.Empty; return true;
        }
        public bool DeleteGrowingZone(int zoneId)
        {
            var zone=GrowingZone(zoneId); if(zone==null) return Reject("InvalidGrowingZone");
            foreach(var c in zone.Cells) { CancelProductionOwners(c.Id); c.Exists=false; }
            zone.Exists=false; availabilityRevision++; LastCommandError=string.Empty; return true;
        }
        private void GrowCrops()
        {
            foreach(var c in crops) if(c.Exists && c.Stage==CropStage.Growing)
            {
                c.GrowthTicks=FarmingRules.NextGrowthTicks(c.GrowthTicks,c.PlantedTick,Tick);
                if(c.GrowthTicks>=FarmingRules.GrowthDuration)
                { c.Stage=CropStage.Mature; c.Reason=WorkReasonCode.None; c.RequestOrder=++requestSequence; availabilityRevision++; }
            }
        }
        private IReadOnlyList<GridPosition> ApproachSite(GridPosition start,GridPosition site,out GridPosition cell)
        {
            IReadOnlyList<GridPosition> best=null; cell=site;
            foreach(var d in Directions)
            {
                var p=new GridPosition(site.X+d.X,site.Y+d.Y); var path=FindPath(start,p);
                if(path!=null && (best==null || path.Count<best.Count)) { best=path; cell=p; }
            }
            return best;
        }
        private bool Clearance(BlueprintState b)
        {
            if(!ConstructionRules.BlocksMovement(b.Kind)) return true;
            foreach(var pawn in Colonists) if(pawn.Position==b.Position) return false;
            foreach(var r in Resources) if(r.Exists && r.Amount>0 && r.Position==b.Position) return false;
            return true;
        }
        private void PrepareProductionOptions(WorkReadContext context,ColonistState pawn)
        {
            var options=new List<FeatureWorkOption>();
            foreach(var b in blueprints) if(b.Exists)
            {
                b.Reason=WorkReasonCode.None;
                if(b.OwnerId.HasValue) { b.Reason=WorkReasonCode.TargetReserved; continue; }
                var approach=ApproachSite(pawn.Position,b.Position,out var cell);
                if(approach==null) { b.Reason=WorkReasonCode.Unreachable; continue; }
                if(b.DeliveredWood>=b.Cost)
                {
                    if(!Clearance(b)) { b.Reason=WorkReasonCode.AwaitingClearance; continue; }
                    options.Add(new FeatureWorkOption(new WorkCandidate(context,WorkCategory.Build,WorkStep.Construct,b.Id,null,cell,b.RequestOrder,approach),WorkReasonCode.None));
                    continue;
                }
                ResourceState best=null; IReadOnlyList<GridPosition> route=null;
                foreach(var r in Resources)
                {
                    if(!r.Exists||r.Kind!=ResourceKind.Wood||r.Amount<=0||r.OwnerId.HasValue||r.CreatedTick>=Tick) continue;
                    var path=FindPath(pawn.Position,r.Position); if(path==null) continue;
                    if(ApproachSite(r.Position,b.Position,out _) == null) continue;
                    if(best==null||path.Count<route.Count||path.Count==route.Count&&r.Id<best.Id) { best=r; route=path; }
                }
                if(best==null) { b.Reason=WorkReasonCode.MissingMaterials; continue; }
                options.Add(new FeatureWorkOption(new WorkCandidate(context,WorkCategory.Build,WorkStep.DeliverBuild,b.Id,best.Id,best.Position,b.RequestOrder,route),WorkReasonCode.None));
            }
            foreach(var b in buildings) if(b.Exists && b.DemolitionDesignated)
            {
                b.Reason=WorkReasonCode.None;
                if(b.OwnerId.HasValue) { b.Reason=WorkReasonCode.TargetReserved; continue; }
                var route=ApproachSite(pawn.Position,b.Position,out var cell);
                if(route==null) { b.Reason=WorkReasonCode.Unreachable; continue; }
                options.Add(new FeatureWorkOption(new WorkCandidate(context,WorkCategory.Build,WorkStep.Demolish,b.Id,null,cell,b.RequestOrder,route),WorkReasonCode.None));
            }
            foreach(var c in crops) if(c.Exists)
            {
                if(c.OwnerId.HasValue) continue;
                c.Reason=WorkReasonCode.None;
                var zone=GrowingZone(c.ZoneId);
                if(c.Stage==CropStage.Growing) continue;
                if(c.Stage==CropStage.Empty&&!zone.SowingEnabled) {c.Reason=WorkReasonCode.Disabled;continue;}
                var distance=FindPathLength(pawn.Position,c.Position); if(distance<0) {c.Reason=WorkReasonCode.Unreachable;continue;}
                var step=c.Stage==CropStage.Empty?WorkStep.Sow:WorkStep.Harvest;
                options.Add(new FeatureWorkOption(new WorkCandidate(context,WorkCategory.Farm,step,c.Id,null,c.Position,c.RequestOrder,distance),WorkReasonCode.None));
            }
            PrepareCookingOptions(context,pawn,options);
            context.SetFeatureInputs(options);
        }
        private bool StartProductionWork(ColonistState pawn,WorkCandidate candidate)
        {
            if(candidate.Step==WorkStep.Cook||candidate.Step==WorkStep.DeliverCook)return StartCookingWork(pawn,candidate);
            var route = candidate.Route;
            InteractionClaim claim=null;
            if(candidate.Step==WorkStep.DeliverBuild)
            {
                var b=Blueprint(candidate.TargetId);
                if(b==null||b.OwnerId.HasValue||b.DeliveredWood>=b.Cost||!candidate.SourceId.HasValue ||
                    !resourcesById.TryGetValue(candidate.SourceId.Value,out var r)||!r.Exists||r.Kind!=ResourceKind.Wood||r.Amount<=0||r.OwnerId.HasValue) return false;
                EndLife(pawn); r.Reserve(pawn.Id,pawn.ActionId,Math.Min(10,Math.Min(r.Amount,b.Cost-b.DeliveredWood)));
                pawn.WorkTargetId=r.Id; pawn.PendingDeliveryTargetId=b.Id; pawn.FeatureStep=candidate.Step;
                pawn.WorkActivity=WorkActivity.GoingToBuildPickup;
            }
            else
            {
                if(candidate.Step==WorkStep.Construct)
                { var b=Blueprint(candidate.TargetId); if(b==null||b.DeliveredWood<b.Cost||!Clearance(b)) return false; claim=b.Claim; }
                else if(candidate.Step==WorkStep.Demolish)
                { var b=Building(candidate.TargetId); if(b==null||!b.DemolitionDesignated) return false; claim=b.Claim; }
                else if(candidate.Step==WorkStep.Sow || candidate.Step==WorkStep.Harvest)
                {
                    var c=Crop(candidate.TargetId);
                    if(c==null||c.Stage!=(candidate.Step==WorkStep.Sow?CropStage.Empty:CropStage.Mature)||candidate.Step==WorkStep.Sow&&!GrowingZone(c.ZoneId).SowingEnabled) return false;
                    // The caller already verified the candidate's tick, origin and terrain revision.
                    route = ReadPath(pawn.Position,candidate.WorkCell);
                    if(route==null || route.Count!=candidate.PathLength) return false;
                    claim=c.Claim;
                }
                else return false;
                if(claim.OwnerId.HasValue) return false;
                EndLife(pawn); claim.Acquire(pawn.Id,pawn.ActionId);
                var crop=Crop(candidate.TargetId); if(crop!=null) crop.Reason=WorkReasonCode.None;
                pawn.FeatureStep=candidate.Step; pawn.WorkTargetId=candidate.TargetId; pawn.WorkActivity=WorkActivity.GoingToFeature;
            }
            pawn.SetRoute(candidate.WorkCell,route); pawn.WorkReason=string.Empty; availabilityRevision++; return true;
        }
        private void ClearProductionWork(ColonistState pawn)
        {
            foreach(var b in blueprints) b.Claim.Release(pawn.Id,pawn.ActionId);
            foreach(var b in buildings) b.Claim.Release(pawn.Id,pawn.ActionId);
            foreach(var c in crops) c.Claim.Release(pawn.Id,pawn.ActionId);
            pawn.FeatureStep=null; pawn.PendingDeliveryTargetId=null; pawn.IsCookingDelivery=false;
        }
        private bool ValidProductionWork(ColonistState pawn)
        {
            if(!pawn.WorkTargetId.HasValue||!pawn.FeatureStep.HasValue) return false;
            if(pawn.FeatureStep==WorkStep.Cook||pawn.FeatureStep==WorkStep.DeliverCook)return ValidCookingWork(pawn);
            if(pawn.FeatureStep==WorkStep.DeliverBuild)
            {
                var b=Blueprint(pawn.PendingDeliveryTargetId??-1); if(b==null||b.DeliveredWood>=b.Cost) return false;
                if(pawn.WorkActivity==WorkActivity.GoingToBuildPickup||pawn.WorkActivity==WorkActivity.PickingUpBuild)
                    return resourcesById.TryGetValue(pawn.WorkTargetId.Value,out var r)&&r.Exists&&r.Amount>0&&r.Claim.Matches(pawn.Id,pawn.ActionId)&&!blockedSet.Contains(r.Position);
                return b.Claim.Matches(pawn.Id,pawn.ActionId)&&pawn.CargoAmount>0;
            }
            if(pawn.FeatureStep==WorkStep.Construct) {var b=Blueprint(pawn.WorkTargetId.Value); return b!=null&&b.DeliveredWood>=b.Cost&&b.Claim.Matches(pawn.Id,pawn.ActionId);}
            if(pawn.FeatureStep==WorkStep.Demolish) {var b=Building(pawn.WorkTargetId.Value); return b!=null&&b.DemolitionDesignated&&b.Claim.Matches(pawn.Id,pawn.ActionId);}
            var c=Crop(pawn.WorkTargetId.Value);
            return c!=null&&c.Claim.Matches(pawn.Id,pawn.ActionId)&&!blockedSet.Contains(c.Position)&&
                (pawn.FeatureStep==WorkStep.Sow?c.Stage==CropStage.Empty&&GrowingZone(c.ZoneId).SowingEnabled:c.Stage==CropStage.Mature);
        }
        private void FinishProduction(ColonistState pawn) { EndLife(pawn); cancelledThisTick.Add(pawn.Id); availabilityRevision++; }
        private void ProgressProductionWork(ColonistState pawn)
        {
            if(pawn.FeatureStep==WorkStep.Cook||pawn.FeatureStep==WorkStep.DeliverCook){ProgressCookingWork(pawn);return;}
            if(pawn.WorkActivity==WorkActivity.PickingUpBuild)
            {
                var b=Blueprint(pawn.PendingDeliveryTargetId.Value); var r=resourcesById[pawn.WorkTargetId.Value];
                var route=ApproachSite(pawn.Position,b.Position,out var cell);
                var amount=Math.Min(10,Math.Min(r.Amount,b.Cost-b.DeliveredWood));
                if(route==null||amount<=0||!b.Claim.Acquire(pawn.Id,pawn.ActionId)) { FinishProduction(pawn); return; }
                r.TransferOut(amount); r.Release(pawn.Id,pawn.ActionId);
                pawn.CargoKind=ResourceKind.Wood; pawn.CargoAmount=amount; pawn.WorkTargetId=b.Id;
                pawn.WorkActivity=WorkActivity.GoingToBuildDelivery; pawn.SetRoute(cell,route); availabilityRevision++; return;
            }
            if(pawn.WorkActivity==WorkActivity.DeliveringBuild)
            {
                var b=Blueprint(pawn.WorkTargetId.Value);
                if(pawn.CargoAmount>b.Cost-b.DeliveredWood) { FinishProduction(pawn); return; }
                b.DeliveredWood+=pawn.CargoAmount; pawn.CargoAmount=0; FinishProduction(pawn); return;
            }
            if(pawn.WorkActivity!=WorkActivity.WorkingFeature) return;
            var id=pawn.WorkTargetId.Value; var step=pawn.FeatureStep.Value;
            var progress=0; var duration=0; var cost=0; var reason=WorkReasonCode.None;
            if(step==WorkStep.Construct)
            { var b=Blueprint(id); progress=b.Progress; duration=b.Duration; cost=b.Cost; if(!Clearance(b)) reason=WorkReasonCode.AwaitingClearance; b.Reason=reason; }
            else if(step==WorkStep.Demolish) {var b=Building(id);progress=b.DemolitionProgress;duration=ConstructionRules.DemolitionDuration;cost=b.Cost;}
            else {var c=Crop(id);progress=step==WorkStep.Sow?c.SowProgress:c.HarvestProgress;duration=step==WorkStep.Sow?FarmingRules.SowDuration:FarmingRules.HarvestDuration;}
            var snapshot=new FeatureWorkSnapshot(pawn.Id,pawn.ActionId,id,step,progress,duration,cost,reason);
            var context=new WorkReadContext(workContextToken,Tick,availabilityRevision,MapRevision,pawn);
            context.SetFeatureInputs(new List<FeatureWorkOption>(),snapshot); context.Seal(new List<WorkCandidate>(),reason);
            var result=step==WorkStep.Construct||step==WorkStep.Demolish?ConstructionRules.EvaluateStep(context,pawn.Id):FarmingRules.EvaluateStep(context,pawn.Id);
            // A rule result is applied only to its current action and unchanged progress, once.
            if(result.Source!=snapshot||pawn.ActionId!=snapshot.ActionId||!ValidProductionWork(pawn)||result.Progress<progress||result.Progress>Math.Min(duration,progress+1)) return;
            if(step==WorkStep.Construct)
            {
                var b=Blueprint(id); if(b.Progress!=snapshot.Progress) return; b.Progress=result.Progress;
                if(result.Completed)
                {
                    if(result.ConsumeAmount!=b.Cost||b.DeliveredWood<b.Cost||!Clearance(b)) return;
                    b.DeliveredWood-=b.Cost; ConstructionConsumedWood+=b.Cost;
                    var built=new BuildingState(NewId(),b.Kind,b.Position);buildings.Add(built); b.Exists=false;
                    if(b.Kind==BuildingKind.Stove) InitializeStove(built);
                    if(b.Kind==BuildingKind.Bed) {var bed=new BedState(new BedSeed(built.Id,built.Position)); mutableBeds.Add(bed);bedsById.Add(bed.Id,bed);}
                    if(ConstructionRules.BlocksMovement(b.Kind)) SetProductionBlocked(b.Position,true);
                    FinishProduction(pawn);
                }
            }
            else if(step==WorkStep.Demolish)
            {
                var b=Building(id); if(b.DemolitionProgress!=snapshot.Progress) return; b.DemolitionProgress=result.Progress;
                if(result.Completed)
                {
                    if(b.Kind==BuildingKind.Stove) RemoveStove(b);
                    b.Exists=false; if(ConstructionRules.BlocksMovement(b.Kind)) SetProductionBlocked(b.Position,false);
                    if(b.Kind==BuildingKind.Bed) bedsById[b.Id].SetExists(false);
                    SpawnResource(ResourceKind.Wood,result.ProduceAmount,b.Position); DemolitionReturnedWood+=result.ProduceAmount;
                    FinishProduction(pawn);
                }
            }
            else
            {
                var c=Crop(id);
                if(step==WorkStep.Sow)
                {
                    if(c.SowProgress!=snapshot.Progress) return; c.SowProgress=result.Progress;
                    if(result.Completed) {c.Stage=CropStage.Growing;c.GrowthTicks=0;c.PlantedTick=Tick;FinishProduction(pawn);}
                }
                else
                {
                    if(c.HarvestProgress!=snapshot.Progress) return;c.HarvestProgress=result.Progress;
                    if(result.Completed)
                    {
                        SpawnResource(ResourceKind.Ingredients,result.ProduceAmount,c.Position);
                        produced[ResourceKind.Ingredients]=ProducedAmount(ResourceKind.Ingredients)+result.ProduceAmount;
                        c.Stage=CropStage.Empty;c.Reason=GrowingZone(c.ZoneId).SowingEnabled?WorkReasonCode.None:WorkReasonCode.Disabled;c.SowProgress=0;c.HarvestProgress=0;c.GrowthTicks=0;c.PlantedTick=-1;c.RequestOrder=++requestSequence;FinishProduction(pawn);
                    }
                }
            }
        }
        private void SetProductionBlocked(GridPosition position,bool blocked)
        {
            if(blocked) {if(blockedSet.Add(position)) blockedTiles.Add(position);}
            else {blockedSet.Remove(position);blockedTiles.Remove(position);}
            MapRevision++; availabilityRevision++;
            foreach(var pawn in Colonists) if((pawn.Status==MovementStatus.Moving||pawn.Status==MovementStatus.Blocked)&&pawn.Destination.HasValue)
                pawn.SetRoute(pawn.Destination.Value,FindPath(pawn.Position,pawn.Destination.Value));
        }
    }
}
