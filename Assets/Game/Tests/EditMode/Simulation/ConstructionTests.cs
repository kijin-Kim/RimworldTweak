using System;
using System.Linq;
using NUnit.Framework;
using RimworldTweak.Simulation;
namespace RimworldTweak.Tests.EditMode
{
    public sealed class ConstructionTests
    {
        private static GridPosition P(int x,int y) => new GridPosition(x,y);
        private static WorldState World(int wood=40, bool two=false, bool needs=false) => WorldState.Create(new ScenarioDefinition(10,10,
            Array.Empty<GridPosition>(), two ? new[]{new ColonistSeed(1,P(1,1),9000,9000),new ColonistSeed(2,P(2,1),9000,9000)} :
            new[]{new ColonistSeed(1,P(1,1),9000,9000)}, wood == 0 ? Array.Empty<ResourceSeed>() : new[]{new ResourceSeed(101,ResourceKind.Wood,wood,P(1,1))}, null,needs));
        private static void Until(WorldState world, Func<bool> condition, int limit=1500)
        { for(var i=0;i<limit&&!condition();i++) world.Step(1); Assert.That(condition(),Is.True,"Expected transition at tick "+world.Tick); }
        private static int Wood(WorldState w) => w.ResourceTotal(ResourceKind.Wood,false)+w.ResourceTotal(ResourceKind.Wood,true)+w.CargoTotal(ResourceKind.Wood)+w.Blueprints.Where(b=>b.Exists).Sum(b=>b.DeliveredWood);
        private static BlueprintState Place(WorldState w,BuildingKind kind=BuildingKind.Wall)
        { Assert.That(w.PlaceBlueprint(kind,P(4,4)),Is.True);return w.Blueprints.Last(b=>b.Exists); }
        private static BuildingState Build(WorldState w,BuildingKind kind=BuildingKind.Wall)
        { Place(w,kind);Until(w,()=>w.Buildings.Any(b=>b.Exists));return w.Buildings.Last(b=>b.Exists); }

        [Test] public void PlacementAndCancellationAreImmediateAndDoNotCreateWood()
        {
            var w=World(); var b=Place(w); var action=w.Colonists[0].ActionId;
            Assert.That(w.Tick,Is.Zero);Assert.That(b.DeliveredWood,Is.Zero);Assert.That(Wood(w),Is.EqualTo(40));
            Assert.That(w.CancelBlueprint(b.Id),Is.True);Assert.That(b.Exists,Is.False);
            Assert.That(w.Tick,Is.Zero);Assert.That(w.Colonists[0].ActionId,Is.EqualTo(action));Assert.That(Wood(w),Is.EqualTo(40));
            Assert.That(w.CancelBlueprint(b.Id),Is.False);
        }
        [Test] public void PlacementRejectsOverlapResourcesStorageAndStoveServiceCell()
        {
            var w=World();Assert.That(w.PlaceBlueprint(BuildingKind.Wall,P(1,1)),Is.False);
            Assert.That(w.PlaceBlueprint(BuildingKind.Stove,P(4,0)),Is.False);
            Assert.That(w.QueueCreateStorage(P(5,4),P(5,4)),Is.True);
            Assert.That(w.PlaceBlueprint(BuildingKind.Stove,P(5,5)),Is.False);
            Assert.That(w.PlaceBlueprint(BuildingKind.Stove,P(4,4)),Is.True);
            Assert.That(w.PlaceBlueprint(BuildingKind.Wall,P(4,3)),Is.False);
            Assert.That(w.QueueCreateStorage(P(4,3),P(4,3)),Is.False);
            Assert.That(w.CreateGrowingZone(P(4,3),P(4,3)),Is.False);
            Assert.That(w.PlaceBlueprint((BuildingKind)999,P(7,7)),Is.False);
            Assert.That(w.Blueprints.Count(b=>b.Exists),Is.EqualTo(1)); Assert.That(w.Tick,Is.Zero);
        }
        [Test] public void MissingWoodNeverProducesFreeBuilding()
        {
            var w=World(0);var b=Place(w);w.Step(200);
            Assert.That(w.Buildings.Any(x=>x.Exists),Is.False);Assert.That(b.Progress,Is.Zero);
            Assert.That(b.Reason,Is.EqualTo(WorkReasonCode.MissingMaterials));Assert.That(Wood(w),Is.Zero);
        }
        [Test] public void EveryKindConsumesExactCostAndCreatesOneFacility()
        {
            foreach(BuildingKind kind in Enum.GetValues(typeof(BuildingKind)))
            {
                var w=World();var facility=Build(w,kind);w.Step(100);
                Assert.That(facility.Kind,Is.EqualTo(kind));Assert.That(w.Buildings.Count(b=>b.Exists),Is.EqualTo(1));
                Assert.That(Wood(w),Is.EqualTo(40-ConstructionRules.Cost(kind)));
                Assert.That(w.BlockedTiles.Contains(facility.Position),Is.EqualTo(ConstructionRules.BlocksMovement(kind)));
                if(kind==BuildingKind.Bed) Assert.That(w.Beds.Any(b=>b.Id==facility.Id&&b.Exists&&b.Position==facility.Position),Is.True);
            }
        }
        [Test] public void DeliveryCapsCargoAndDoesNotOverfillWhenTwoPawnsCompete()
        {
            var w=World(two:true);var b=Place(w,BuildingKind.Stove);
            for(var i=0;i<600&&b.Exists;i++)
            {
                w.Step(1);Assert.That(w.Colonists.All(p=>p.CargoAmount<=10),Is.True);
                Assert.That(b.DeliveredWood,Is.LessThanOrEqualTo(15));
                if(b.Exists) Assert.That(Wood(w),Is.EqualTo(40));
            }
            Assert.That(b.Exists,Is.False);Assert.That(Wood(w),Is.EqualTo(25));
        }
        [Test] public void CancelDuringCargoDropsOnceAndReleasesAllClaims()
        {
            var w=World();var b=Place(w,BuildingKind.Stove);Until(w,()=>w.Colonists[0].CargoAmount>0);
            var tick=w.Tick;Assert.That(w.CancelBlueprint(b.Id),Is.True);
            Assert.That(w.Tick,Is.EqualTo(tick));Assert.That(w.Colonists[0].CargoAmount,Is.Zero);
            Assert.That(Wood(w),Is.EqualTo(40));Assert.That(w.Resources.All(r=>!r.OwnerId.HasValue),Is.True);Assert.That(b.OwnerId,Is.Null);
            w.CancelBlueprint(b.Id);Assert.That(Wood(w),Is.EqualTo(40));
        }
        [Test] public void DraftDuringConstructionPreservesProgressAndDeliveredMaterials()
        {
            var w=World();var b=Place(w);Until(w,()=>b.Progress==10);
            var tick=w.Tick;var wood=Wood(w);w.ToggleDraft(1);
            Assert.That(w.Tick,Is.EqualTo(tick));Assert.That(b.OwnerId,Is.Null);Assert.That(b.Progress,Is.EqualTo(10));
            Assert.That(b.DeliveredWood,Is.EqualTo(2));w.Step(20);Assert.That(b.Progress,Is.EqualTo(10));Assert.That(Wood(w),Is.EqualTo(wood));
            w.ToggleDraft(1);Until(w,()=>!b.Exists);Assert.That(Wood(w),Is.EqualTo(38));
        }
        [Test] public void ConstructionCompletesOnlyAtDurationBoundary()
        {
            var w=World();var b=Place(w);Until(w,()=>b.Progress==1);
            w.Step(b.Duration-2);Assert.That(b.Progress,Is.EqualTo(b.Duration-1));Assert.That(b.Exists,Is.True);
            Assert.That(w.TryGetWorkFeedback(1,out var work),Is.True);
            Assert.That(work.Kind,Is.EqualTo(WorkFeedbackKind.Construct));
            Assert.That(work.TargetCell,Is.EqualTo(b.Position));
            Assert.That(work.Progress,Is.EqualTo(b.Progress));Assert.That(work.Duration,Is.EqualTo(b.Duration));
            w.Step(1);Assert.That(b.Exists,Is.False);Assert.That(w.Buildings.Count(x=>x.Exists),Is.EqualTo(1));
            Assert.That(w.TryGetWorkFeedback(1,out _),Is.False);
            Assert.That(Wood(w),Is.EqualTo(38));w.Step(5);Assert.That(Wood(w),Is.EqualTo(38));
        }
        [Test] public void OccupiedWallCompletionWaitsAndResumesWithoutDeletingPawn()
        {
            var w=World(two:true);w.ToggleDraft(2);w.QueueMove(2,P(4,4));Until(w,()=>w.Colonists[1].Position==P(4,4));
            var b=Place(w);w.Step(300);Assert.That(b.Exists,Is.True);Assert.That(w.BlockedTiles.Contains(P(4,4)),Is.False);
            Assert.That(b.Reason,Is.EqualTo(WorkReasonCode.AwaitingClearance));Assert.That(Wood(w),Is.EqualTo(40));
            w.QueueMove(2,P(5,4));Until(w,()=>b.Progress>0);
            Assert.That(w.TryGetWorkFeedback(1,out _),Is.True);
            w.QueueMove(2,P(4,4));Until(w,()=>w.Colonists[1].Position==b.Position);
            var blockedProgress=b.Progress;
            Assert.That(w.TryGetWorkFeedback(1,out _),Is.False);
            w.Step(10);Assert.That(b.Progress,Is.EqualTo(blockedProgress));
            w.QueueMove(2,P(5,4));Until(w,()=>w.Colonists[1].Position==P(5,4));
            Assert.That(w.TryGetWorkFeedback(1,out var resumed),Is.True);
            Assert.That(resumed.Progress,Is.EqualTo(blockedProgress));
            Until(w,()=>!b.Exists);Assert.That(w.Colonists[1].Position,Is.EqualTo(P(5,4)));
            Assert.That(Wood(w),Is.EqualTo(38));
        }
        [Test] public void DemolitionWaitsThirtyTicksAndRefundsOnlyOnce()
        {
            var w=World();var building=Build(w);Assert.That(w.SetDemolition(building.Id,true),Is.True);
            Until(w,()=>building.DemolitionProgress==1);w.Step(28);
            Assert.That(w.TryGetWorkFeedback(1,out var work),Is.True);
            Assert.That(work.Kind,Is.EqualTo(WorkFeedbackKind.Demolish));
            Assert.That(work.Progress,Is.EqualTo(29));Assert.That(work.Duration,Is.EqualTo(30));
            Assert.That(building.Exists,Is.True);Assert.That(building.DemolitionProgress,Is.EqualTo(29));Assert.That(Wood(w),Is.EqualTo(38));
            w.Step(1);Assert.That(building.Exists,Is.False);Assert.That(Wood(w),Is.EqualTo(40));Assert.That(w.BlockedTiles.Contains(building.Position),Is.False);
            Assert.That(w.SetDemolition(building.Id,true),Is.False);w.Step(40);Assert.That(Wood(w),Is.EqualTo(40));
        }
        [Test] public void DemolitionCancelRetainsBuildingWithoutEarlyRefund()
        {
            var w=World();var building=Build(w,BuildingKind.Door);w.SetDemolition(building.Id,true);
            Until(w,()=>building.DemolitionProgress==10);var tick=w.Tick;w.SetDemolition(building.Id,false);
            Assert.That(w.Tick,Is.EqualTo(tick));Assert.That(building.OwnerId,Is.Null);Assert.That(building.Exists,Is.True);
            w.Step(80);Assert.That(building.Exists,Is.True);Assert.That(Wood(w),Is.EqualTo(36));
        }
        [Test] public void ConstructedBedSupportsSleepAndDemolitionImmediatelyReleasesSleeper()
        {
            var w=World(needs:true);var building=Build(w,BuildingKind.Bed);
            Until(w,()=>w.Colonists[0].LifeActivity==LifeActivity.SleepingInBed,1900);
            var bed=w.Beds.Single(b=>b.Id==building.Id);Assert.That(bed.OwnerId,Is.EqualTo(1));
            var tick=w.Tick;w.SetDemolition(building.Id,true);
            Assert.That(w.Tick,Is.EqualTo(tick));Assert.That(bed.OwnerId,Is.Null);
            Assert.That(w.Colonists[0].LifeActivity,Is.Not.EqualTo(LifeActivity.SleepingInBed));
        }
        [Test] public void DraftOnSourceApproachReleasesClaimAndOldActionCannotCancelRestart()
        {
            var w=WorldState.Create(new ScenarioDefinition(10,10,Array.Empty<GridPosition>(),new[]{new ColonistSeed(1,P(1,1))},
                new[]{new ResourceSeed(101,ResourceKind.Wood,20,P(7,1))},null,false));
            var b=Place(w);w.Step(1);var oldAction=w.Colonists[0].ActionId;
            Assert.That(w.Resources[0].OwnerId,Is.EqualTo(1));Assert.That(w.Colonists[0].CargoAmount,Is.Zero);
            w.ToggleDraft(1);Assert.That(w.Resources[0].OwnerId,Is.Null);Assert.That(b.OwnerId,Is.Null);Assert.That(Wood(w),Is.EqualTo(20));
            w.ToggleDraft(1);w.Step(1);var current=w.Colonists[0].ActionId;
            w.QueueCancelLife(1,oldAction);Assert.That(w.Colonists[0].ActionId,Is.EqualTo(current));
            Assert.That(w.Resources[0].OwnerId,Is.EqualTo(1));
        }
        [Test] public void CancelAfterDeliveryRefundsOnlyDeliveredWood()
        {
            var w=World();var b=Place(w,BuildingKind.Bed);Until(w,()=>b.DeliveredWood==10);
            Assert.That(w.Colonists[0].CargoAmount,Is.Zero);Assert.That(Wood(w),Is.EqualTo(40));
            w.CancelBlueprint(b.Id);Assert.That(Wood(w),Is.EqualTo(40));Assert.That(b.DeliveredWood,Is.Zero);
            Assert.That(w.Resources.Where(r=>r.Exists&&r.Kind==ResourceKind.Wood).Sum(r=>r.Amount),Is.EqualTo(40));
            w.Step(20);Assert.That(w.Buildings.Any(x=>x.Exists),Is.False);
        }
        [Test] public void HungerInterruptsConstructionWithoutLosingSiteProgressOrWood()
        {
            var w=WorldState.Create(new ScenarioDefinition(10,10,Array.Empty<GridPosition>(),new[]{new ColonistSeed(1,P(1,1),4000,9000)},
                new[]{new ResourceSeed(101,ResourceKind.Wood,20,P(1,1)),new ResourceSeed(102,ResourceKind.Meal,1,P(1,1))},null,true));
            var b=Place(w,BuildingKind.Bed);Until(w,()=>b.Progress>0);
            Until(w,()=>w.Colonists[0].IsLifeBusy);var progress=b.Progress;
            Assert.That(b.Exists,Is.True);Assert.That(b.OwnerId,Is.Null);Assert.That(b.DeliveredWood,Is.EqualTo(10));Assert.That(Wood(w),Is.EqualTo(20));
            w.Step(1);Assert.That(b.Progress,Is.EqualTo(progress));Until(w,()=>w.Colonists[0].MealsEaten==1);
            Until(w,()=>!b.Exists);Assert.That(Wood(w),Is.EqualTo(10));
        }
        [Test] public void CompletedConstructionRuleCannotConsumeOrRefundAgain()
        {
            var w=World();var pawn=w.Colonists[0];
            foreach(var step in new[]{WorkStep.Construct,WorkStep.Demolish})
            {
                var duration=step==WorkStep.Construct?40:30;
                foreach(var progress in new[]{duration,duration+1})
                {
                    var context=new WorkReadContext(new object(),w.Tick,0,w.MapRevision,pawn);
                    var source=new FeatureWorkSnapshot(pawn.Id,pawn.ActionId,200,step,progress,duration,2,WorkReasonCode.None);
                    context.SetFeatureInputs(new System.Collections.Generic.List<FeatureWorkOption>(),source);
                    context.Seal(new System.Collections.Generic.List<WorkCandidate>(),WorkReasonCode.None);
                    var result=ConstructionRules.EvaluateStep(context,pawn.Id);
                    Assert.That(result.Completed,Is.False);Assert.That(result.ConsumeAmount,Is.Zero);Assert.That(result.ProduceAmount,Is.Zero);
                    Assert.That(result.Progress,Is.EqualTo(progress));Assert.That(result.Reason,Is.EqualTo(WorkReasonCode.InvalidTarget));
                    Assert.That(w.Tick,Is.Zero);Assert.That(Wood(w),Is.EqualTo(40));
                }
            }
        }
    }
}
