using System;
using System.Collections.Generic;
namespace RimworldTweak.Simulation
{
    public sealed partial class WorldState
    {
        private readonly List<CookingOrderState> cookingOrders = new List<CookingOrderState>();
        private readonly List<CookingOutputState> cookingOutputs = new List<CookingOutputState>();
        public IReadOnlyList<CookingOrderState> CookingOrders => cookingOrders.AsReadOnly();
        public IReadOnlyList<CookingOutputState> CookingOutputs => cookingOutputs.AsReadOnly();
        public int CookingConsumedIngredients { get; private set; }
        public int CookingProducedMeals { get; private set; }
        public int CookingInputTotal { get { var n=0; foreach(var o in cookingOrders) if(o.Exists)n+=o.InputIngredients;return n; } }
        private CookingOrderState CookingOrder(int stoveId) => cookingOrders.Find(o=>o.Exists&&o.StoveId==stoveId);
        private CookingOutputState CookingOutput(int stoveId) => cookingOutputs.Find(o=>o.Exists&&o.StoveId==stoveId);
        private int MealTotal => ResourceTotal(ResourceKind.Meal,false)+ResourceTotal(ResourceKind.Meal,true)+CargoTotal(ResourceKind.Meal);
        private void InitializeStove(BuildingState b)
        {
            cookingOrders.Add(new CookingOrderState(NewId(),b.Id,ConstructionRules.ServiceCell(b.Position),b.Claim){RequestOrder=++requestSequence});
            cookingOutputs.Add(new CookingOutputState(NewId(),b.Id,ConstructionRules.ServiceCell(b.Position)));
        }
        public bool SetCookingOrder(int stoveId,bool enabled,int targetMeals)
        {
            var b=Building(stoveId);
            if(b==null||b.Kind!=BuildingKind.Stove||targetMeals<0||targetMeals>CookingRules.MaximumTarget)return Reject("InvalidCookingOrder");
            var o=CookingOrder(stoveId);
            if(o==null) {o=new CookingOrderState(NewId(),stoveId,ConstructionRules.ServiceCell(b.Position),b.Claim){RequestOrder=++requestSequence};cookingOrders.Add(o);}
            o.Enabled=enabled;o.TargetMeals=targetMeals;
            ReconcileCookingCycles(false);availabilityRevision++;LastCommandError=string.Empty;return true;
        }
        public bool DeleteCookingOrder(int stoveId)
        {
            var o=CookingOrder(stoveId);if(o==null)return Reject("InvalidCookingOrder");
            CancelProductionOwners(stoveId);
            if(o.InputIngredients>0)SpawnResource(ResourceKind.Ingredients,o.InputIngredients,o.ServiceCell);
            o.InputIngredients=0;o.Progress=0;o.ActiveCycle=false;o.Exists=false;
            availabilityRevision++;LastCommandError=string.Empty;return true;
        }
        private void RemoveStove(BuildingState b)
        {
            if(CookingOrder(b.Id)!=null)DeleteCookingOrder(b.Id);
            var output=CookingOutput(b.Id);
            if(output!=null) {if(output.Stack!=null){CancelProductionOwners(output.Stack.Id);output.Stack.DetachStorage();}output.Exists=false;}
        }
        private void ReconcileCookingCycles(bool allowStart)
        {
            cookingOrders.Sort((a,b)=>{var n=a.StoveId.CompareTo(b.StoveId);return n!=0?n:a.Id.CompareTo(b.Id);});
            var count=0;var meals=MealTotal;
            foreach(var o in cookingOrders)
            {
                if(!o.Exists)continue;
                var b=Building(o.StoveId);
                var allowed=b!=null&&!b.DemolitionDesignated&&o.Enabled&&meals+count<o.TargetMeals;
                if(!allowed)
                {
                    if(o.ActiveCycle){CancelProductionOwners(o.StoveId);o.ActiveCycle=false;}
                    o.Reason=b==null||b.DemolitionDesignated?WorkReasonCode.InvalidTarget:!o.Enabled?WorkReasonCode.Disabled:WorkReasonCode.TargetReached;
                    continue;
                }
                var feasible=AnyWorkerReaches(WorkCategory.Cook,o.ServiceCell,false);
                var staffed=false;foreach(var policy in workPolicies)if(policy.GetPriority(WorkCategory.Cook)>0)staffed=true;
                if(!staffed||!feasible)
                {if(o.ActiveCycle){CancelProductionOwners(o.StoveId);o.ActiveCycle=false;}o.Reason=!staffed?WorkReasonCode.NoWorker:WorkReasonCode.Unreachable;continue;}
                if(o.ActiveCycle)count++;
                else if(allowStart)
                {
                    var output=CookingOutput(o.StoveId);
                    if(output==null||output.Amount>=CookingOutputState.Capacity){o.Reason=WorkReasonCode.OutputFull;continue;}
                    var material=o.InputIngredients;
                    foreach(var resource in Resources)if(resource.Exists&&resource.Kind==ResourceKind.Ingredients)material+=resource.Amount;
                    foreach(var pawn in Colonists)if(pawn.CargoKind==ResourceKind.Ingredients)material+=pawn.CargoAmount;
                    if(material<2){o.Reason=WorkReasonCode.MissingMaterials;continue;}
                    if(o.InputIngredients<2)
                    {
                        var materialReason=MaterialDiagnostic(WorkCategory.Cook,ResourceKind.Ingredients,o.ServiceCell,false);
                        if(materialReason!=WorkReasonCode.None){o.Reason=materialReason;continue;}
                    }
                    o.ActiveCycle=true;o.RequestOrder=++requestSequence;count++;
                }
            }
        }
        private void PrepareCookingOptions(WorkReadContext context,ColonistState pawn,List<FeatureWorkOption> options)
        {
            foreach(var o in cookingOrders)
            {
                if(!o.Exists||!o.ActiveCycle)continue;
                if(o.OwnerId.HasValue){o.Reason=WorkReasonCode.TargetReserved;continue;}
                var route=FindPath(pawn.Position,o.ServiceCell);
                if(route==null){o.Reason=WorkReasonCode.Unreachable;continue;}
                var output=CookingOutput(o.StoveId);
                if(output.Amount>=CookingOutputState.Capacity){o.Reason=WorkReasonCode.OutputFull;continue;}
                if(output.OwnerId.HasValue){o.Reason=WorkReasonCode.TargetReserved;continue;}
                o.Reason=WorkReasonCode.None;
                if(o.InputIngredients>=2)
                    options.Add(new FeatureWorkOption(new WorkCandidate(context,WorkCategory.Cook,WorkStep.Cook,o.StoveId,null,o.ServiceCell,o.RequestOrder,route),WorkReasonCode.None));
                else
                {
                    ResourceState best=null;IReadOnlyList<GridPosition> bestRoute=null;
                    foreach(var resource in Resources)
                    {
                        if(!resource.Exists||resource.Kind!=ResourceKind.Ingredients||resource.Amount<=0||resource.OwnerId.HasValue||resource.CreatedTick>=Tick)continue;
                        var path=FindPath(pawn.Position,resource.Position);
                        if(path==null||FindPath(resource.Position,o.ServiceCell)==null)continue;
                        if(best==null||path.Count<bestRoute.Count||path.Count==bestRoute.Count&&resource.Id<best.Id){best=resource;bestRoute=path;}
                    }
                    if(best==null){o.Reason=WorkReasonCode.MissingMaterials;continue;}
                    options.Add(new FeatureWorkOption(new WorkCandidate(context,WorkCategory.Cook,WorkStep.DeliverCook,o.StoveId,best.Id,best.Position,o.RequestOrder,bestRoute),WorkReasonCode.None));
                }
            }
        }
        private bool StartCookingWork(ColonistState pawn,WorkCandidate candidate)
        {
            var o=CookingOrder(candidate.TargetId);if(o==null||!o.ActiveCycle||o.OwnerId.HasValue)return false;
            if(candidate.Step==WorkStep.DeliverCook)
            {
                if(o.InputIngredients>=2||!candidate.SourceId.HasValue||!resourcesById.TryGetValue(candidate.SourceId.Value,out var resource)||!resource.Exists||resource.Kind!=ResourceKind.Ingredients||resource.Amount<=0||resource.OwnerId.HasValue)return false;
                EndLife(pawn);resource.Reserve(pawn.Id,pawn.ActionId,Math.Min(resource.Amount,2-o.InputIngredients));
                pawn.WorkTargetId=resource.Id;pawn.PendingDeliveryTargetId=o.StoveId;pawn.WorkActivity=WorkActivity.GoingToBuildPickup;
            }
            else
            {
                if(o.InputIngredients<2)return false;
                EndLife(pawn);o.Claim.Acquire(pawn.Id,pawn.ActionId);pawn.WorkTargetId=o.StoveId;pawn.WorkActivity=WorkActivity.GoingToFeature;
            }
            pawn.FeatureStep=candidate.Step;pawn.SetRoute(candidate.WorkCell,candidate.Route);pawn.WorkReason=string.Empty;availabilityRevision++;return true;
        }
        private bool ValidCookingWork(ColonistState pawn)
        {
            var o=CookingOrder(pawn.PendingDeliveryTargetId??pawn.WorkTargetId??-1);
            if(o==null||!o.ActiveCycle||!o.Enabled||Building(o.StoveId)==null||Building(o.StoveId).DemolitionDesignated)return false;
            if(pawn.FeatureStep==WorkStep.DeliverCook)
            {
                if(o.InputIngredients>=2)return false;
                if(pawn.WorkActivity==WorkActivity.GoingToBuildPickup||pawn.WorkActivity==WorkActivity.PickingUpBuild)
                    return resourcesById.TryGetValue(pawn.WorkTargetId.Value,out var resource)&&resource.Exists&&resource.Amount>0&&resource.Claim.Matches(pawn.Id,pawn.ActionId)&&!blockedSet.Contains(resource.Position);
                return o.Claim.Matches(pawn.Id,pawn.ActionId)&&pawn.CargoAmount>0;
            }
            return o.InputIngredients>=2&&o.Claim.Matches(pawn.Id,pawn.ActionId);
        }
        private void ProgressCookingWork(ColonistState pawn)
        {
            var o=CookingOrder(pawn.PendingDeliveryTargetId??pawn.WorkTargetId.Value);
            if(pawn.WorkActivity==WorkActivity.PickingUpBuild)
            {
                var resource=resourcesById[pawn.WorkTargetId.Value];var route=FindPath(pawn.Position,o.ServiceCell);
                var amount=Math.Min(resource.Amount,2-o.InputIngredients);
                if(route==null||amount<=0||!o.Claim.Acquire(pawn.Id,pawn.ActionId)){FinishProduction(pawn);return;}
                resource.TransferOut(amount);resource.Release(pawn.Id,pawn.ActionId);
                pawn.CargoKind=ResourceKind.Ingredients;pawn.CargoAmount=amount;pawn.WorkTargetId=o.StoveId;
                pawn.WorkActivity=WorkActivity.GoingToBuildDelivery;pawn.SetRoute(o.ServiceCell,route);availabilityRevision++;return;
            }
            if(pawn.WorkActivity==WorkActivity.DeliveringBuild)
            {
                if(pawn.CargoAmount>2-o.InputIngredients){FinishProduction(pawn);return;}
                o.InputIngredients+=pawn.CargoAmount;pawn.CargoAmount=0;FinishProduction(pawn);return;
            }
            if(pawn.WorkActivity!=WorkActivity.WorkingFeature)return;
            var output=CookingOutput(o.StoveId);
            var reason=output.Amount>=CookingOutputState.Capacity?WorkReasonCode.OutputFull:output.OwnerId.HasValue?WorkReasonCode.TargetReserved:WorkReasonCode.None;
            o.Reason=reason;
            var snapshot=new FeatureWorkSnapshot(pawn.Id,pawn.ActionId,o.StoveId,WorkStep.Cook,o.Progress,40,2,reason);
            var context=new WorkReadContext(workContextToken,Tick,availabilityRevision,MapRevision,pawn);
            context.SetFeatureInputs(new List<FeatureWorkOption>(),snapshot);context.Seal(new List<WorkCandidate>(),reason);
            var result=CookingRules.EvaluateStep(context,pawn.Id);
            if(result.Source!=snapshot||!ValidCookingWork(pawn)||o.Progress!=snapshot.Progress||pawn.ActionId!=snapshot.ActionId)return;
            if(result.Progress<o.Progress||result.Progress>Math.Min(40,o.Progress+1))return;
            if(result.Completed)
            {
                if(result.ConsumeAmount!=2||result.ProduceAmount!=1||output.Amount>=50||!output.Claim.Acquire(pawn.Id,pawn.ActionId))return;
                if(output.Stack==null||!output.Stack.Exists)
                {output.Stack=SpawnResource(ResourceKind.Meal,0,output.Position);output.Stack.AttachClaim(output.Claim);}
                output.Stack.TransferIn(1);output.Stack.CreatedTick=Tick;output.Claim.Release(pawn.Id,pawn.ActionId);
                o.InputIngredients-=2;o.Progress=0;o.ActiveCycle=false;o.CompletedMeals++;
                CookingConsumedIngredients+=2;CookingProducedMeals++;produced[ResourceKind.Meal]=ProducedAmount(ResourceKind.Meal)+1;
                FinishProduction(pawn);ReconcileCookingCycles(false);
            }
            else o.Progress=result.Progress;
        }
    }
}
