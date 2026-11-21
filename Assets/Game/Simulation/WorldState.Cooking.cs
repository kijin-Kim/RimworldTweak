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
        private CookingOrderState CookingOrder(int stoveId) =>
            cookingOrders.Find(o=>o.Exists&&o.StoveId==stoveId&&o.ActiveCycle)
            ?? cookingOrders.Find(o=>o.Exists&&o.StoveId==stoveId);
        private CookingOutputState CookingOutput(int stoveId) => cookingOutputs.Find(o=>o.Exists&&o.StoveId==stoveId);
        private static WorkReasonCode CookingOutputReason(CookingOutputState output)
        {
            if (output == null) return WorkReasonCode.InvalidTarget;
            if (output.Amount >= CookingOutputState.Capacity) return WorkReasonCode.OutputFull;
            return output.OwnerId.HasValue ? WorkReasonCode.TargetReserved : WorkReasonCode.None;
        }
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
            return DeleteCookingBill(o.Id);
        }
        public bool DeleteCookingBill(int billId)
        {
            var o=cookingOrders.Find(x=>x.Exists&&x.Id==billId);
            if(o==null)return Reject("InvalidCookingOrder");
            if(o.ActiveCycle)CancelCookingWorkers(o.StoveId);
            if(o.InputIngredients>0)SpawnResource(ResourceKind.Ingredients,o.InputIngredients,o.ServiceCell);
            o.InputIngredients=0;o.Progress=0;o.ActiveCycle=false;o.Exists=false;
            availabilityRevision++;LastCommandError=string.Empty;return true;
        }
        private void RemoveStove(BuildingState b)
        {
            foreach(var order in cookingOrders.ToArray()) if(order.Exists&&order.StoveId==b.Id)DeleteCookingBill(order.Id);
            var output=CookingOutput(b.Id);
            if(output!=null) {if(output.Stack!=null){CancelProductionOwners(output.Stack.Id);output.Stack.DetachStorage();}output.Exists=false;}
        }
        private void CancelCookingWorkers(int stoveId)
        {
            foreach (var pawn in Colonists)
                if ((pawn.FeatureStep == WorkStep.Cook || pawn.FeatureStep == WorkStep.DeliverCook) &&
                    (pawn.WorkTargetId == stoveId || pawn.PendingDeliveryTargetId == stoveId))
                    FinishProduction(pawn);
        }

        private void ReconcileCookingCycles(bool allowStart)
        {
            cookingOrders.Sort((a,b)=>{var n=a.StoveId.CompareTo(b.StoveId);return n!=0?n:a.Priority!=b.Priority?a.Priority.CompareTo(b.Priority):a.Id.CompareTo(b.Id);});
            var count=0;var meals=MealTotal;var busyStoves=new HashSet<int>();
            foreach(var o in cookingOrders)
            {
                if(!o.Exists)continue;
                var b=Building(o.StoveId);
                var demand=NeedsCooking(o, meals+count);
                var allowed=b!=null&&b.Durability>0&&!b.DemolitionDesignated&&o.Enabled&&demand;
                if(busyStoves.Contains(o.StoveId))
                {
                    if(o.ActiveCycle){CancelCookingWorkers(o.StoveId);o.ActiveCycle=false;}
                    o.Reason=WorkReasonCode.TargetReserved;
                    continue;
                }
                if(!allowed)
                {
                    if(o.ActiveCycle){CancelCookingWorkers(o.StoveId);o.ActiveCycle=false;}
                    o.Reason=b==null||b.Durability==0||b.DemolitionDesignated?WorkReasonCode.InvalidTarget:!o.Enabled?WorkReasonCode.Disabled:WorkReasonCode.TargetReached;
                    continue;
                }
                var feasible=AnyWorkerReaches(WorkCategory.Cook,o.ServiceCell,false);
                var staffed=false;foreach(var policy in workPolicies)if(policy.GetPriority(WorkCategory.Cook)>0)staffed=true;
                if(o.WorkerId.HasValue)
                {
                    staffed=false;feasible=false;
                    foreach(var policy in workPolicies)
                        if(policy.PawnId==o.WorkerId.Value&&policy.GetPriority(WorkCategory.Cook)>0)staffed=true;
                    foreach(var pawn in Colonists)
                        if(pawn.Id==o.WorkerId.Value&&!pawn.IsDrafted&&FindPath(pawn.Position,o.ServiceCell)!=null)feasible=true;
                }
                if(!staffed||!feasible)
                {if(o.ActiveCycle){CancelCookingWorkers(o.StoveId);o.ActiveCycle=false;}o.Reason=!staffed?WorkReasonCode.NoWorker:WorkReasonCode.Unreachable;continue;}
                if(o.ActiveCycle){count++;busyStoves.Add(o.StoveId);}
                else if(allowStart)
                {
                    var output=CookingOutput(o.StoveId);
                    if(output==null||output.Amount>=CookingOutputState.Capacity){o.Reason=WorkReasonCode.OutputFull;continue;}
                    if(o.InputIngredients<2&&!HasCookingMaterial(o)){o.Reason=WorkReasonCode.MissingMaterials;continue;}
                    if(o.WorkerId.HasValue && !System.Linq.Enumerable.Any(Colonists,p=>p.Id==o.WorkerId.Value))
                    {o.Reason=WorkReasonCode.NoWorker;continue;}
                    var material=o.InputIngredients;
                    foreach(var resource in Resources)if(resource.Exists&&resource.Kind==ResourceKind.Ingredients)material+=resource.Amount;
                    foreach(var pawn in Colonists)if(pawn.CargoKind==ResourceKind.Ingredients)material+=pawn.CargoAmount;
                    if(material<2){o.Reason=WorkReasonCode.MissingMaterials;continue;}
                    if(o.InputIngredients<2)
                    {
                        var materialReason=MaterialDiagnostic(WorkCategory.Cook,ResourceKind.Ingredients,o.ServiceCell,false);
                        if(materialReason!=WorkReasonCode.None){o.Reason=materialReason;continue;}
                    }
                    o.ActiveCycle=true;o.RequestOrder=++requestSequence;count++;busyStoves.Add(o.StoveId);
                }
            }
        }
        private static bool NeedsCooking(CookingOrderState order, int meals) =>
            order.RepeatMode == CookingRepeatMode.Forever ||
            (order.RepeatMode == CookingRepeatMode.RepeatCount ? order.RemainingCount > 0 : meals < order.TargetMeals);

        public int AddCookingBill(int stoveId)
        {
            var building=Building(stoveId);
            if(building==null||building.Kind!=BuildingKind.Stove){Reject("InvalidCookingOrder");return -1;}
            var priority=0;
            foreach(var bill in cookingOrders)if(bill.Exists&&bill.StoveId==stoveId)priority=Math.Max(priority,bill.Priority+1);
            var order=new CookingOrderState(NewId(),stoveId,ConstructionRules.ServiceCell(building.Position),building.Claim)
            {Priority=priority,Enabled=true,RepeatMode=CookingRepeatMode.RepeatCount,RequestOrder=++requestSequence};
            cookingOrders.Add(order);availabilityRevision++;LastCommandError=string.Empty;return order.Id;
        }
        public bool ConfigureCookingBill(int billId,bool enabled,CookingRepeatMode repeatMode,int amount,
            int? workerId,bool allowIngredients,int radius,CookingDeliveryMode deliveryMode,int? destinationZoneId)
        {
            var order=cookingOrders.Find(o=>o.Exists&&o.Id==billId);
            if(order==null||!Enum.IsDefined(typeof(CookingRepeatMode),repeatMode)||
                !Enum.IsDefined(typeof(CookingDeliveryMode),deliveryMode)||amount<0||amount>99||radius<0||radius>999||
                (workerId.HasValue&&!System.Linq.Enumerable.Any(Colonists,p=>p.Id==workerId.Value))||
                (deliveryMode==CookingDeliveryMode.SpecificStorage&&(!destinationZoneId.HasValue||Zone(destinationZoneId.Value)==null)))
                return Reject("InvalidCookingOrder");
            var destination = deliveryMode == CookingDeliveryMode.SpecificStorage ? destinationZoneId : null;
            var sameQuantity = repeatMode == CookingRepeatMode.Forever ||
                amount == (repeatMode == CookingRepeatMode.RepeatCount ? order.RemainingCount : order.TargetMeals);
            if (order.Enabled == enabled && order.RepeatMode == repeatMode && sameQuantity &&
                order.WorkerId == workerId && order.AllowIngredients == allowIngredients &&
                order.IngredientRadius == radius && order.DeliveryMode == deliveryMode && order.DestinationZoneId == destination)
            {
                LastCommandError = string.Empty;
                return true;
            }
            if(order.ActiveCycle){CancelProductionOwners(order.StoveId);order.ActiveCycle=false;}
            order.Enabled=enabled;order.RepeatMode=repeatMode;
            if(repeatMode==CookingRepeatMode.RepeatCount)order.RemainingCount=amount;
            else if(repeatMode==CookingRepeatMode.TargetStock)order.TargetMeals=amount;
            order.WorkerId=workerId;order.AllowIngredients=allowIngredients;order.IngredientRadius=radius;
            order.DeliveryMode=deliveryMode;order.DestinationZoneId=destination;
            ReconcileCookingCycles(false);availabilityRevision++;LastCommandError=string.Empty;return true;
        }
        public bool MoveCookingBill(int billId,int direction)
        {
            var order=cookingOrders.Find(o=>o.Exists&&o.Id==billId);
            if(order==null||(direction!=-1&&direction!=1))return Reject("InvalidCookingOrder");
            var list=cookingOrders.FindAll(o=>o.Exists&&o.StoveId==order.StoveId);
            list.Sort((a,b)=>a.Priority!=b.Priority?a.Priority.CompareTo(b.Priority):a.Id.CompareTo(b.Id));
            var index=list.IndexOf(order);var next=index+direction;
            if(next<0||next>=list.Count)return false;
            list.RemoveAt(index);list.Insert(next,order);
            for(var i=0;i<list.Count;i++)list[i].Priority=i;
            availabilityRevision++;LastCommandError=string.Empty;return true;
        }
        private bool CookingMaterialAllowed(CookingOrderState order,ResourceState resource)
        {
            var dx=resource.Position.X-order.ServiceCell.X;var dy=resource.Position.Y-order.ServiceCell.Y;
            return order.AllowIngredients&&(long)dx*dx+(long)dy*dy<=(long)order.IngredientRadius*order.IngredientRadius;
        }
        private bool HasCookingMaterial(CookingOrderState order)
        {
            var amount=order.InputIngredients;
            foreach(var resource in Resources)
                if(resource.Exists&&resource.Kind==ResourceKind.Ingredients&&!resource.OwnerId.HasValue&&CookingMaterialAllowed(order,resource)
                    &&FindPath(resource.Position,order.ServiceCell)!=null)amount+=resource.Amount;
            return amount>=2;
        }
        private void PrepareCookingOptions(WorkReadContext context,ColonistState pawn,List<FeatureWorkOption> options)
        {
            foreach(var o in cookingOrders)
            {
                if(!o.Exists||!o.ActiveCycle||(o.WorkerId.HasValue&&o.WorkerId.Value!=pawn.Id))continue;
                if(o.OwnerId.HasValue){o.Reason=WorkReasonCode.TargetReserved;continue;}
                var route=FindPath(pawn.Position,o.ServiceCell);
                if(route==null){o.Reason=WorkReasonCode.Unreachable;continue;}
                var output=CookingOutput(o.StoveId);
                o.Reason=CookingOutputReason(output);
                if(o.Reason!=WorkReasonCode.None)continue;
                if(o.InputIngredients>=2)
                    options.Add(new FeatureWorkOption(new WorkCandidate(context,WorkCategory.Cook,WorkStep.Cook,o.StoveId,null,o.ServiceCell,o.RequestOrder,route),WorkReasonCode.None));
                else
                {
                    ResourceState best=null;IReadOnlyList<GridPosition> bestRoute=null;
                    foreach(var resource in Resources)
                    {
                        if(!resource.Exists||resource.Kind!=ResourceKind.Ingredients||resource.Amount<=0||resource.OwnerId.HasValue||resource.CreatedTick>=Tick||!CookingMaterialAllowed(o,resource))continue;
                        var path=FindPath(pawn.Position,resource.Position);
                        if(path==null||FindPath(resource.Position,o.ServiceCell)==null)continue;
                        if(best==null||Walking.RouteCost(pawn.Position, path)<Walking.RouteCost(pawn.Position, bestRoute)||Walking.RouteCost(pawn.Position, path)==Walking.RouteCost(pawn.Position, bestRoute)&&resource.Id<best.Id){best=resource;bestRoute=path;}
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
                if(o.InputIngredients<2||(o.WorkerId.HasValue&&o.WorkerId.Value!=pawn.Id))return false;
                EndLife(pawn);o.Claim.Acquire(pawn.Id,pawn.ActionId);pawn.WorkTargetId=o.StoveId;pawn.WorkActivity=WorkActivity.GoingToFeature;
            }
            pawn.FeatureStep=candidate.Step;pawn.SetRoute(candidate.WorkCell,candidate.Route);pawn.WorkReason=string.Empty;availabilityRevision++;return true;
        }
        private bool ValidCookingWork(ColonistState pawn)
        {
            var o=CookingOrder(pawn.PendingDeliveryTargetId??pawn.WorkTargetId??-1);
            if(o==null||!o.ActiveCycle||!o.Enabled||Building(o.StoveId)==null||(Building(o.StoveId).DemolitionDesignated||Building(o.StoveId).Durability==0))return false;
            if(pawn.FeatureStep==WorkStep.DeliverCook)
            {
                if(o.InputIngredients>=2)return false;
                if(pawn.WorkActivity==WorkActivity.GoingToBuildPickup||pawn.WorkActivity==WorkActivity.PickingUpBuild)
                    return resourcesById.TryGetValue(pawn.WorkTargetId.Value,out var resource)&&resource.Exists&&resource.Amount>0&&resource.Claim.Matches(pawn.Id,pawn.ActionId)&&!blockedSet.Contains(resource.Position);
                return o.Claim.Matches(pawn.Id,pawn.ActionId)&&pawn.CargoAmount>0;
            }
            return o.InputIngredients>=2&&o.Claim.Matches(pawn.Id,pawn.ActionId);
        }
        private void DeliverCookingOutput(ColonistState pawn,CookingOrderState order,CookingOutputState output)
        {
            if(order.DeliveryMode==CookingDeliveryMode.DropOnFloor)return;
            if(order.DeliveryMode==CookingDeliveryMode.SpecificStorage&&!order.DestinationZoneId.HasValue)return;
            var slot=ChooseStorage(ResourceKind.Meal,pawn.Position,out var route,
                order.DeliveryMode==CookingDeliveryMode.SpecificStorage?order.DestinationZoneId:null);
            if(slot==null||!slot.Claim.Acquire(pawn.Id,pawn.ActionId))return;
            output.Stack.TransferOut(1);
            pawn.CargoKind=ResourceKind.Meal;pawn.CargoAmount=1;pawn.IsCookingDelivery=true;
            pawn.WorkTargetId=slot.Id;pawn.WorkActivity=WorkActivity.GoingToStore;
            pawn.SetRoute(slot.Position,route);
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
            var reason=CookingOutputReason(output);
            o.Reason=reason;
            var snapshot=new FeatureWorkSnapshot(pawn.Id,pawn.ActionId,o.StoveId,WorkStep.Cook,o.Progress,CookingRules.Duration,CookingRules.IngredientsPerMeal,reason);
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
                if(o.RepeatMode==CookingRepeatMode.RepeatCount)o.RemainingCount--;
                CookingConsumedIngredients+=2;CookingProducedMeals++;produced[ResourceKind.Meal]=ProducedAmount(ResourceKind.Meal)+1;
                FinishProduction(pawn);
                DeliverCookingOutput(pawn,o,output);
                ReconcileCookingCycles(false);
            }
            else o.Progress=result.Progress;
        }
    }
}
