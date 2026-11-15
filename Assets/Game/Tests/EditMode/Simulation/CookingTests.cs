using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RimworldTweak.Simulation;
namespace RimworldTweak.Tests.EditMode
{
    public sealed class CookingTests
    {
        private static GridPosition P(int x,int y)=>new GridPosition(x,y);
        private static WorldState World(int ingredients=12,int pawns=1,bool needs=false,int meals=0)
        {
            var resources=new List<ResourceSeed>{new ResourceSeed(101,ResourceKind.Wood,40,P(1,1))};
            if(ingredients>0)resources.Add(new ResourceSeed(102,ResourceKind.Ingredients,ingredients,P(2,1)));
            if(meals>0)resources.Add(new ResourceSeed(103,ResourceKind.Meal,meals,P(1,2)));
            return WorldState.Create(new ScenarioDefinition(10,10,Array.Empty<GridPosition>(),
                Enumerable.Range(1,pawns).Select(id=>new ColonistSeed(id,P(id,1),10000,10000)),resources,null,needs));
        }
        private static void Until(WorldState w,Func<bool> condition,int limit=2000)
        {for(var i=0;i<limit&&!condition();i++)w.Step(1);Assert.That(condition(),Is.True,"Cooking transition missing at Tick "+w.Tick);}
        private static BuildingState BuildStove(WorldState w,GridPosition? position=null)
        {
            var p=position??P(4,4);Assert.That(w.PlaceBlueprint(BuildingKind.Stove,p),Is.True);
            Until(w,()=>w.Buildings.Any(b=>b.Exists&&b.Kind==BuildingKind.Stove&&b.Position==p));
            return w.Buildings.Single(b=>b.Exists&&b.Kind==BuildingKind.Stove&&b.Position==p);
        }
        private static CookingOrderState Order(WorldState w,BuildingState b)=>w.CookingOrders.Single(o=>o.Exists&&o.StoveId==b.Id);
        private static CookingOutputState Output(WorldState w,BuildingState b)=>w.CookingOutputs.Single(o=>o.Exists&&o.StoveId==b.Id);
        private static int Total(WorldState w,ResourceKind kind)=>w.ResourceTotal(kind,false)+w.ResourceTotal(kind,true)+w.CargoTotal(kind);
        private static int Ingredients(WorldState w)=>Total(w,ResourceKind.Ingredients)+w.CookingOrders.Where(o=>o.Exists).Sum(o=>o.InputIngredients);

        [Test] public void ConstructedStoveStartsDisabledAndOrderChangesAreImmediate()
        {
            var w=World();var b=BuildStove(w);var o=Order(w,b);var tick=w.Tick;
            Assert.That(o.Enabled,Is.False);Assert.That(o.TargetMeals,Is.EqualTo(12));Assert.That(o.InputIngredients,Is.Zero);
            Assert.That(Output(w,b).Position,Is.EqualTo(P(4,3)));
            Assert.That(w.SetCookingOrder(b.Id,true,4),Is.True);Assert.That(w.Tick,Is.EqualTo(tick));Assert.That(o.TargetMeals,Is.EqualTo(4));
            Assert.That(w.SetCookingOrder(b.Id,true,100),Is.False);Assert.That(w.SetCookingOrder(b.Id,true,-1),Is.False);
            Assert.That(o.TargetMeals,Is.EqualTo(4));Assert.That(w.SetCookingOrder(-1,true,1),Is.False);
        }
        [Test] public void OneMealConsumesTwoIngredientsOnlyAtFortyTickBoundary()
        {
            var w=World();var b=BuildStove(w);var o=Order(w,b);w.SetCookingOrder(b.Id,true,1);
            Until(w,()=>o.Progress==1);Assert.That(o.InputIngredients,Is.EqualTo(2));Assert.That(Ingredients(w),Is.EqualTo(12));
            w.Step(38);Assert.That(o.Progress,Is.EqualTo(39));Assert.That(Total(w,ResourceKind.Meal),Is.Zero);
            w.Step(1);Assert.That(Total(w,ResourceKind.Meal),Is.EqualTo(1));Assert.That(Ingredients(w),Is.EqualTo(10));
            Assert.That(w.CookingConsumedIngredients,Is.EqualTo(2));Assert.That(w.CookingProducedMeals,Is.EqualTo(1));
            w.Step(100);Assert.That(w.CookingProducedMeals,Is.EqualTo(1));Assert.That(Output(w,b).Amount,Is.EqualTo(1));
        }
        [Test] public void DraftPreservesCookingProgressAndInputAndResumes()
        {
            var w=World();var b=BuildStove(w);var o=Order(w,b);w.SetCookingOrder(b.Id,true,1);Until(w,()=>o.Progress==10);
            var tick=w.Tick;var action=w.Colonists[0].ActionId;w.ToggleDraft(1);
            Assert.That(w.Tick,Is.EqualTo(tick));Assert.That(o.OwnerId,Is.Null);Assert.That(o.Progress,Is.EqualTo(10));Assert.That(o.InputIngredients,Is.EqualTo(2));
            w.Step(25);Assert.That(o.Progress,Is.EqualTo(10));Assert.That(Ingredients(w),Is.EqualTo(12));
            w.ToggleDraft(1);w.Step(1);w.QueueCancelLife(1,action);Until(w,()=>w.CookingProducedMeals==1);
            Assert.That(Ingredients(w),Is.EqualTo(10));
        }
        [Test] public void StoppingOrZeroTargetKeepsInputAndProgressWithoutProducing()
        {
            var w=World();var b=BuildStove(w);var o=Order(w,b);w.SetCookingOrder(b.Id,true,3);Until(w,()=>o.Progress==10);
            var tick=w.Tick;w.SetCookingOrder(b.Id,false,3);Assert.That(w.Tick,Is.EqualTo(tick));Assert.That(o.OwnerId,Is.Null);
            w.Step(60);Assert.That(o.Progress,Is.EqualTo(10));Assert.That(o.ActiveCycle,Is.False);Assert.That(Ingredients(w),Is.EqualTo(12));
            w.SetCookingOrder(b.Id,true,0);w.Step(60);Assert.That(o.Progress,Is.EqualTo(10));Assert.That(w.CookingProducedMeals,Is.Zero);
            w.SetCookingOrder(b.Id,true,1);Until(w,()=>w.CookingProducedMeals==1);
        }
        [Test] public void DeletingOrderReturnsInputOnceAndPreservesFinishedMeals()
        {
            var w=World();var b=BuildStove(w);var o=Order(w,b);w.SetCookingOrder(b.Id,true,2);
            Until(w,()=>w.CookingProducedMeals==1&&o.Progress>0);var tick=w.Tick;
            Assert.That(w.DeleteCookingOrder(b.Id),Is.True);Assert.That(w.Tick,Is.EqualTo(tick));Assert.That(o.Exists,Is.False);
            Assert.That(Ingredients(w),Is.EqualTo(10));Assert.That(Total(w,ResourceKind.Meal),Is.EqualTo(1));
            Assert.That(w.DeleteCookingOrder(b.Id),Is.False);Assert.That(Ingredients(w),Is.EqualTo(10));
            Assert.That(w.SetCookingOrder(b.Id,false,12),Is.True);var fresh=Order(w,b);
            Assert.That(fresh.Id,Is.Not.EqualTo(o.Id));Assert.That(fresh.Progress,Is.Zero);Assert.That(fresh.InputIngredients,Is.Zero);
        }
        [Test] public void CancelDuringIngredientDeliveryDropsOnceWithoutDuplication()
        {
            var w=World();var b=BuildStove(w);w.SetCookingOrder(b.Id,true,1);
            Until(w,()=>w.Colonists[0].CargoKind==ResourceKind.Ingredients&&w.Colonists[0].CargoAmount>0);
            Assert.That(w.Colonists[0].CargoAmount,Is.EqualTo(2));var tick=w.Tick;w.DeleteCookingOrder(b.Id);
            Assert.That(w.Tick,Is.EqualTo(tick));Assert.That(w.Colonists[0].CargoAmount,Is.Zero);Assert.That(Ingredients(w),Is.EqualTo(12));
            Assert.That(w.Resources.All(r=>!r.OwnerId.HasValue),Is.True);w.QueueCancelLife(1);Assert.That(Ingredients(w),Is.EqualTo(12));
        }
        [Test] public void TwoStovesRespectOneMealTargetAndLowerTargetKeepsLowestIdCycle()
        {
            var w=World(pawns:2);var first=BuildStove(w);var second=BuildStove(w,P(7,4));
            w.SetCookingOrder(first.Id,true,1);w.SetCookingOrder(second.Id,true,1);w.Step(400);
            Assert.That(w.CookingProducedMeals,Is.EqualTo(1));Assert.That(Ingredients(w),Is.EqualTo(10));
            w.SetCookingOrder(first.Id,true,3);w.SetCookingOrder(second.Id,true,3);
            Until(w,()=>w.CookingOrders.Count(o=>o.Exists&&o.ActiveCycle)==2);
            w.SetCookingOrder(first.Id,true,2);w.SetCookingOrder(second.Id,true,2);
            var active=w.CookingOrders.Where(o=>o.Exists&&o.ActiveCycle).ToArray();Assert.That(active.Length,Is.EqualTo(1));
            Assert.That(active[0].StoveId,Is.EqualTo(first.Id));w.Step(400);Assert.That(w.CookingProducedMeals,Is.EqualTo(2));
        }
        [Test] public void FullOutputPausesConsumptionAndFreedSpaceResumes()
        {
            var w=World(ingredients:120);var b=BuildStove(w);var o=Order(w,b);w.SetCookingOrder(b.Id,true,99);
            Until(w,()=>Output(w,b).Amount==50,5000);w.Step(80);
            Assert.That(w.CookingProducedMeals,Is.EqualTo(50));Assert.That(w.CookingConsumedIngredients,Is.EqualTo(100));
            Assert.That(o.Reason,Is.EqualTo(WorkReasonCode.OutputFull));Assert.That(Ingredients(w),Is.EqualTo(20));
            w.QueueSetResourceAmount(Output(w,b).Stack.Id,49);w.Step(1);Until(w,()=>w.CookingProducedMeals==51);
            Assert.That(Output(w,b).Amount,Is.EqualTo(50));Assert.That(w.CookingConsumedIngredients,Is.EqualTo(102));
        }
        [Test] public void OutputStackAndSlotShareClaimSoCompletionCannotWriteIntoReservedFood()
        {
            var w=World();var b=BuildStove(w);var o=Order(w,b);w.SetCookingOrder(b.Id,true,2);
            Until(w,()=>w.CookingProducedMeals==1&&o.Progress>0);var output=Output(w,b);
            Assert.That(output.Stack.Reserve(999,1,1),Is.True);Assert.That(output.OwnerId,Is.EqualTo(999));
            var consumed=w.CookingConsumedIngredients;w.Step(80);Assert.That(w.CookingProducedMeals,Is.EqualTo(1));Assert.That(w.CookingConsumedIngredients,Is.EqualTo(consumed));
            output.Stack.Release(999,1);Until(w,()=>w.CookingProducedMeals==2);
        }
        [Test] public void StoveDemolitionReturnsWoodAndInputAndKeepsCompletedOutput()
        {
            var w=World();var b=BuildStove(w);var o=Order(w,b);w.SetCookingOrder(b.Id,true,2);
            Until(w,()=>w.CookingProducedMeals==1&&o.Progress>0);var tick=w.Tick;w.SetDemolition(b.Id,true);
            Assert.That(w.Tick,Is.EqualTo(tick));Assert.That(o.OwnerId,Is.Null);Until(w,()=>!b.Exists);
            Assert.That(Ingredients(w),Is.EqualTo(10));Assert.That(Total(w,ResourceKind.Meal),Is.EqualTo(1));
            Assert.That(Total(w,ResourceKind.Wood),Is.EqualTo(40));Assert.That(w.DemolitionReturnedWood,Is.EqualTo(15));
            w.Step(100);Assert.That(Total(w,ResourceKind.Wood),Is.EqualTo(40));Assert.That(w.CookingProducedMeals,Is.EqualTo(1));
        }
        [Test] public void ProducedMealCanBeEatenFromActualConstructedStove()
        {
            var w=World(ingredients:2,needs:true);var b=BuildStove(w);w.SetCookingOrder(b.Id,true,1);
            Until(w,()=>w.CookingProducedMeals==1);var producedId=Output(w,b).Stack.Id;
            Until(w,()=>w.Resources.Single(r=>r.Id==producedId).ConsumedAmount==1,1500);
            Assert.That(w.Colonists[0].MealsEaten,Is.EqualTo(1));Assert.That(w.CookingConsumedIngredients,Is.EqualTo(2));
        }
        [Test] public void HarvestedIngredientsEnterConstructedStoveAndOutputCanBeHauled()
        {
            var w=World(ingredients:0);var b=BuildStove(w);w.SetCookingOrder(b.Id,true,1);
            Assert.That(w.CreateGrowingZone(P(6,2),P(6,2)),Is.True);var zone=w.GrowingZones.Single(z=>z.Exists);
            Until(w,()=>w.Crops.Single().Stage==CropStage.Growing);w.SetSowingEnabled(zone.Id,false);
            Until(w,()=>w.CookingProducedMeals==1,3000);
            Assert.That(w.ProducedAmount(ResourceKind.Ingredients),Is.EqualTo(6));Assert.That(w.CookingConsumedIngredients,Is.EqualTo(2));Assert.That(Ingredients(w),Is.EqualTo(4));
            Assert.That(w.QueueCreateStorage(P(8,8),P(8,8)),Is.True);var storage=w.StorageZones.Single();
            w.QueueStorageFilter(storage.Id,ResourceKind.Wood,false);w.QueueStorageFilter(storage.Id,ResourceKind.Ingredients,false);
            Until(w,()=>w.ResourceTotal(ResourceKind.Meal,true)==1);Assert.That(Output(w,b).Amount,Is.Zero);
            Assert.That(Total(w,ResourceKind.Meal),Is.EqualTo(1));
        }
        [Test] public void CookingRulesArePureAndRejectRepeatedCompletedResults()
        {
            var w=World();var pawn=w.Colonists[0];
            foreach(var progress in new[]{39,40})
            {
                var context=new WorkReadContext(new object(),w.Tick,0,w.MapRevision,pawn);
                var source=new FeatureWorkSnapshot(pawn.Id,pawn.ActionId,200,WorkStep.Cook,progress,40,2,WorkReasonCode.None);
                context.SetFeatureInputs(new List<FeatureWorkOption>(),source);context.Seal(new List<WorkCandidate>(),WorkReasonCode.None);
                var result=CookingRules.EvaluateStep(context,pawn.Id);
                Assert.That(result.Completed,Is.EqualTo(progress==39));Assert.That(result.ConsumeAmount,Is.EqualTo(progress==39?2:0));
                Assert.That(result.ProduceAmount,Is.EqualTo(progress==39?1:0));Assert.That(source.Progress,Is.EqualTo(progress));Assert.That(w.Tick,Is.Zero);
            }
        }
        [Test] public void RecreatedOrderStillAllocatesAndRetainsCyclesInStoveIdOrder()
        {
            var w=World(pawns:2);var first=BuildStove(w);var second=BuildStove(w,P(7,4));
            Assert.That(first.Id,Is.LessThan(second.Id));w.DeleteCookingOrder(first.Id);w.SetCookingOrder(first.Id,false,12);
            Assert.That(Order(w,first).Id,Is.GreaterThan(Order(w,second).Id));
            w.SetCookingOrder(first.Id,true,1);w.SetCookingOrder(second.Id,true,1);w.Step(1);
            Assert.That(Order(w,first).ActiveCycle,Is.True);Assert.That(Order(w,second).ActiveCycle,Is.False);
            w.SetCookingOrder(first.Id,true,2);w.SetCookingOrder(second.Id,true,2);
            Until(w,()=>Order(w,first).ActiveCycle&&Order(w,second).ActiveCycle,150);
            Assert.That(w.CookingProducedMeals,Is.Zero);
            Assert.That(Order(w,first).ActiveCycle,Is.True);Assert.That(Order(w,second).ActiveCycle,Is.True);
            w.SetCookingOrder(first.Id,true,1);w.SetCookingOrder(second.Id,true,1);
            Assert.That(Order(w,first).ActiveCycle,Is.True);Assert.That(Order(w,second).ActiveCycle,Is.False);
        }
        [Test] public void ReusedOutputStackIsNotHaulableUntilTickAfterProduction()
        {
            var w=World(ingredients:6,pawns:2);var b=BuildStove(w);w.SetCookingOrder(b.Id,true,1);
            Until(w,()=>w.CookingProducedMeals==1);var output=Output(w,b);var stack=output.Stack;
            w.QueueCreateStorage(P(8,8),P(8,8));var storage=w.StorageZones.Single();
            w.QueueStorageFilter(storage.Id,ResourceKind.Wood,false);w.QueueStorageFilter(storage.Id,ResourceKind.Ingredients,false);
            Until(w,()=>w.ResourceTotal(ResourceKind.Meal,true)==1);Assert.That(stack.Amount,Is.Zero);
            w.SetCookingOrder(b.Id,true,2);Until(w,()=>w.CookingProducedMeals==2);
            Assert.That(output.Stack,Is.SameAs(stack));Assert.That(stack.Amount,Is.EqualTo(1));
            Assert.That(stack.CreatedTick,Is.EqualTo(w.Tick-1));Assert.That(stack.OwnerId,Is.Null);
            w.Step(1);Assert.That(stack.OwnerId,Is.Not.Null);Assert.That(Total(w,ResourceKind.Meal),Is.EqualTo(2));
        }
        [Test] public void UnreachableLowerIdStoveDoesNotReserveTargetAwayFromReachableStove()
        {
            var w=World();var first=BuildStove(w);var second=BuildStove(w,P(7,4));
            Assert.That(w.QueueSetBlocked(Order(w,first).ServiceCell,true),Is.True);w.Step(1);
            w.SetCookingOrder(first.Id,true,1);w.SetCookingOrder(second.Id,true,1);w.Step(1);
            Assert.That(Order(w,first).ActiveCycle,Is.False);Assert.That(Order(w,second).ActiveCycle,Is.True);
            Until(w,()=>w.CookingProducedMeals==1);
            Assert.That(Order(w,first).CompletedMeals,Is.Zero);Assert.That(Order(w,second).CompletedMeals,Is.EqualTo(1));
            Assert.That(Ingredients(w),Is.EqualTo(10));w.Step(100);Assert.That(w.CookingProducedMeals,Is.EqualTo(1));
        }
    }
}
