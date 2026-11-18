using System;
using System.Linq;
using System.IO;
using System.Text;
using NUnit.Framework;
using RimworldTweak.Simulation;
using RimworldTweak.Runtime;
using UnityEngine;

namespace RimworldTweak.Tests.EditMode
{
    public sealed class SaveRoundTripTests
    {
        private static GridPosition P(int x, int y) => new GridPosition(x, y);
        private static ScenarioDefinition Scenario(bool needs = false, int nutrition = 10000, int rest = 10000)
            => new ScenarioDefinition(8, 8, Array.Empty<GridPosition>(), new[] { new ColonistSeed(1, P(1, 1), nutrition, rest) },
                new[] { new ResourceSeed(101, ResourceKind.Wood, 40, P(1, 1)), new ResourceSeed(102, ResourceKind.Ingredients, 8, P(1, 2)), new ResourceSeed(103, ResourceKind.Meal, 6, P(1, 1)) },
                new[] { new BedSeed(201, P(1, 1)) }, needs, new[] { new GatherSeed(301, GatherKind.Tree, P(2, 1)) });
        private static void Only(WorldState w, WorkCategory category)
        { foreach (WorkCategory c in Enum.GetValues(typeof(WorkCategory))) w.SetWorkPriority(1, c, c == category ? 1 : 0); }
        private static void Until(SimulationSession session, Func<bool> condition, int limit = 1000)
        { for (var i = 0; i < limit && !condition(); i++) session.Step(1); Assert.That(condition(), Is.True, "Save test transition missing at " + session.World.Tick); }
        private static string State(WorldState w)
        {
            var b = new StringBuilder(); Action<object[]> row = values => b.AppendLine(string.Join("|", values));
            row(new object[] { w.Width, w.Height, w.Tick, w.MapRevision, w.PathSearchCount, w.PendingCommandCount, w.NeedsEnabled, w.LastCommandError,
                w.ConstructionConsumedWood, w.DemolitionReturnedWood, w.CookingConsumedIngredients, w.CookingProducedMeals });
            row(w.BlockedTiles.Select(p => (object)p).ToArray());
            foreach (var p in w.Colonists) row(new object[] {p.Id,p.Position,p.Destination,string.Join(";",p.Path),p.MovementProgress,p.MovementTicksPerTile,p.Status,
                p.HasPendingCommand,p.PendingDestination,p.IsDrafted,p.ActionId,p.HasSleepIntent,p.WorkActivity,p.WorkTargetId,p.FeatureStep,p.PendingDeliveryTargetId,
                p.CargoKind,p.CargoAmount,p.WorkReason,p.WorkReadyTick,p.NextWorkSearchTick,p.LastWorkRevision,p.Nutrition,p.Rest,p.LifeActivity,p.LifeTargetId,
                p.ActionProgress,p.ActionDuration,p.FoodReason,p.SleepReason,p.MealsEaten,p.RawMealsEaten,p.SleepsCompleted,p.StarvationTicks,
                p.NextFoodSearchTick,p.LastFoodRevision,p.LastFoodPosition,p.NextBedSearchTick,p.LastBedRevision,p.LastBedPosition});
            foreach (var r in w.Resources) row(new object[] {r.Id,r.Kind,r.Position,r.Amount,r.Exists,r.CreatedTick,r.StorageSlotId,r.OwnerId,r.OwnerActionId,r.IsInUse,r.ReservedAmount,
                r.ConsumedAmount,r.AddedAmount,r.RemovedAmount,string.Join(";",r.Reservations.Select(pair=>pair.Key+":"+pair.Value))});
            foreach (var bed in w.Beds) row(new object[] {bed.Id,bed.Position,bed.Exists,bed.OwnerId,bed.OwnerActionId,bed.IsOccupied});
            foreach (var n in w.Gatherables) row(new object[] {n.Id,n.Kind,n.Position,n.Designated,n.Depleted,n.Progress,n.OwnerId,n.OwnerActionId,n.RequestOrder});
            foreach (var z in w.StorageZones) { row(new object[] {z.Id,z.Exists,z.Allows(ResourceKind.Wood),z.Allows(ResourceKind.Ingredients),z.Allows(ResourceKind.Meal)});
                foreach(var slot in z.Slots) row(new object[] {slot.Id,slot.ZoneId,slot.Position,slot.Stack?.Id,slot.Amount,slot.OwnerId,slot.OwnerActionId}); }
            foreach (var p in w.Blueprints) row(new object[] {p.Id,p.Kind,p.Position,p.RequestOrder,p.Exists,p.DeliveredWood,p.Progress,p.Reason,p.OwnerId,p.OwnerActionId});
            foreach (var p in w.Buildings) row(new object[] {p.Id,p.Kind,p.Position,p.Exists,p.DemolitionDesignated,p.DemolitionProgress,p.RequestOrder,p.Reason,p.OwnerId,p.OwnerActionId});
            foreach (var z in w.GrowingZones) row(new object[] {z.Id,z.Exists,z.SowingEnabled,string.Join(";",z.Cells.Select(c=>c.Id))});
            foreach (var c in w.Crops) row(new object[] {c.Id,c.ZoneId,c.Position,c.Exists,c.Stage,c.SowProgress,c.HarvestProgress,c.GrowthTicks,c.PlantedTick,c.RequestOrder,c.Reason,c.OwnerId,c.OwnerActionId});
            foreach (var o in w.CookingOrders) row(new object[] {o.Id,o.StoveId,o.ServiceCell,o.Exists,o.Enabled,o.TargetMeals,o.InputIngredients,o.Progress,o.ActiveCycle,o.RequestOrder,o.Reason,o.CompletedMeals,o.OwnerId,o.OwnerActionId});
            foreach (var o in w.CookingOutputs) row(new object[] {o.Id,o.StoveId,o.Position,o.Exists,o.Stack?.Id,o.Amount,o.OwnerId,o.OwnerActionId});
            foreach (var p in w.WorkPolicies) foreach(WorkCategory c in Enum.GetValues(typeof(WorkCategory))) row(new object[] {p.PawnId,c,p.GetPriority(c)});
            foreach(ResourceKind kind in Enum.GetValues(typeof(ResourceKind))) row(new object[] {kind,w.ProducedAmount(kind)});
            return b.ToString();
        }
        private static SimulationSession Restore(SimulationSession original)
        {
            var before = State(original.World); var capture = original.CaptureSaveData(true, 3);
            Assert.That(capture.Success, Is.True, capture.ErrorCode); Assert.That(State(original.World), Is.EqualTo(before), "Capture changed state");
            var testRoot=Path.GetFullPath(Path.Combine(Application.dataPath,"..","TestResults","SaveRoundTrip"));
            var directory=Path.GetFullPath(Path.Combine(testRoot,Guid.NewGuid().ToString("N")));
            var path=Path.Combine(directory,"settlement-v1.json");
            try
            {
                var files=new SaveFileService();
                var saved=files.Save(path,capture.Data);Assert.That(saved.Success,Is.True,saved.ErrorCode);
                Assert.That(File.Exists(path),Is.True);Assert.That(File.Exists(path+".tmp"),Is.False);
                var loaded=files.Load(path);Assert.That(loaded.Success,Is.True,loaded.ErrorCode);
                Assert.That(loaded.Data,Is.Not.SameAs(capture.Data));
                Assert.That(State(original.World),Is.EqualTo(before),"File round trip changed source");
                Assert.That(SimulationSession.TryRestoreSave(loaded.Data, out var restored, out var error), Is.True, error);
                Assert.That(restored.World, Is.Not.SameAs(original.World)); Assert.That(State(restored.World), Is.EqualTo(before), "Restore lost state");
                Assert.That(restored.PendingSeconds, Is.EqualTo(original.PendingSeconds)); Assert.That(restored.ResetCount, Is.EqualTo(original.ResetCount));
                var legacyBytes=File.ReadAllBytes(path);
                var repository=new SaveRepository(directory);
                var slot=repository.SaveNew("진행 중 작업",capture.Data);
                Assert.That(slot.Success,Is.True,slot.ErrorCode);
                var reopened=new SaveRepository(directory).Load(slot.Slot.SlotId);
                Assert.That(reopened.Success,Is.True,reopened.ErrorCode);
                Assert.That(SimulationSession.TryRestoreSave(reopened.Data,out var slotRestored,out error),Is.True,error);
                Assert.That(State(slotRestored.World),Is.EqualTo(before),"Named slot lost in-progress state");
                Assert.That(slotRestored.PendingSeconds,Is.EqualTo(original.PendingSeconds));
                Assert.That(slotRestored.ResetCount,Is.EqualTo(original.ResetCount));
                CollectionAssert.AreEqual(legacyBytes,File.ReadAllBytes(path),"Named slot changed legacy file");
                // Exercise both file formats under identical subsequent commands/ticks.
                Equivalent(restored,slotRestored,50);
                Assert.That(SimulationSession.TryRestoreSave(reopened.Data,out slotRestored,out error),Is.True,error);
                return slotRestored;
            }
            finally
            {
                if(Directory.Exists(directory))
                {
                    var resolved=Path.GetFullPath(directory);var prefix=testRoot+Path.DirectorySeparatorChar;
                    Assert.That(resolved.StartsWith(prefix,StringComparison.OrdinalIgnoreCase),Is.True);
                    Assert.That(Directory.GetParent(resolved).FullName,Is.EqualTo(testRoot).IgnoreCase);
                    Directory.Delete(resolved,true);
                }
            }
        }
        private static void Equivalent(SimulationSession first, SimulationSession second, int ticks)
        {
            for(var i=0;i<ticks;i++) {first.Step(1);second.Step(1);Assert.That(State(second.World),Is.EqualTo(State(first.World)),"Diverged after "+(i+1)+" ticks");}
        }

        [Test]
        public void MultipleCookingBillsAndCookDeliveryResumeAfterFileRoundTrip()
        {
            var session=new SimulationSession(Scenario());var w=session.World;
            w.PlaceBlueprint(BuildingKind.Stove,P(4,4));
            Until(session,()=>w.Buildings.Any(x=>x.Exists&&x.Kind==BuildingKind.Stove));
            var stove=w.Buildings.Single(x=>x.Exists&&x.Kind==BuildingKind.Stove);
            var first=w.CookingOrders.Single(x=>x.Exists);
            w.ConfigureCookingBill(first.Id,true,CookingRepeatMode.Forever,1,null,false,5,CookingDeliveryMode.DropOnFloor,null);
            w.QueueCreateStorage(P(7,7),P(7,7));var zone=w.StorageZones.Single();
            var second=w.AddCookingBill(stove.Id);
            w.ConfigureCookingBill(second,true,CookingRepeatMode.RepeatCount,2,1,true,20,CookingDeliveryMode.SpecificStorage,zone.Id);
            Only(w,WorkCategory.Cook);
            Until(session,()=>w.CookingProducedMeals==1&&w.Colonists[0].CargoKind==ResourceKind.Meal&&w.Colonists[0].CargoAmount==1);
            var restored=Restore(session);
            var bill=restored.World.CookingOrders.Single(x=>x.Id==second);
            Assert.That(bill.RepeatMode,Is.EqualTo(CookingRepeatMode.RepeatCount));
            Assert.That(bill.RemainingCount,Is.EqualTo(1));
            Assert.That(bill.WorkerId,Is.EqualTo(1));
            Assert.That(bill.IngredientRadius,Is.EqualTo(20));
            Assert.That(bill.DestinationZoneId,Is.EqualTo(zone.Id));
            Equivalent(session,restored,300);
            Assert.That(restored.World.ResourceTotal(ResourceKind.Meal,true),Is.EqualTo(2));
        }

        [Test]
        public void PendingMovesAndPartialTileTravelContinueAtExactlyTheSameTime()
        {
            var s = new SimulationSession(Scenario()); s.AdvanceTime(.03m); s.World.ToggleDraft(1);
            s.World.QueueMove(1,P(5,1)); s.World.QueueMove(1,P(6,1));
            var pending = Restore(s); Equivalent(s,pending,2);
            Assert.That(s.World.Colonists[0].MovementProgress,Is.EqualTo(2));
            var moving = Restore(s); Equivalent(s,moving,35);
            Assert.That(moving.World.Colonists[0].Position,Is.EqualTo(P(6,1)));
        }

        [Test]
        public void EatingAndOccupiedBedRoundTripKeepActionClaimsAndCompletionCounts()
        {
            var eating = new SimulationSession(Scenario(true,3000,9000)); eating.Step(8);
            Assert.That(eating.World.Colonists[0].LifeActivity,Is.EqualTo(LifeActivity.Eating));
            var restoredEating = Restore(eating); Equivalent(eating,restoredEating,30);
            Assert.That(restoredEating.World.Colonists[0].MealsEaten,Is.EqualTo(1));
            var sleeping = new SimulationSession(Scenario(true,9000,2000)); sleeping.Step(8);
            Assert.That(sleeping.World.Beds[0].IsOccupied,Is.True);
            var restoredSleeping = Restore(sleeping); Equivalent(sleeping,restoredSleeping,150);
            Assert.That(restoredSleeping.World.Colonists[0].SleepsCompleted,Is.EqualTo(1));
            var floor=new SimulationSession(new ScenarioDefinition(8,8,Array.Empty<GridPosition>(),
                new[]{new ColonistSeed(1,P(1,1),9000,2000)},Array.Empty<ResourceSeed>(),Array.Empty<BedSeed>(),true));
            floor.Step(8);Assert.That(floor.World.Colonists[0].LifeActivity,Is.EqualTo(LifeActivity.SleepingOnFloor));
            Assert.That(floor.World.Colonists[0].ActionProgress,Is.GreaterThanOrEqualTo(5));
            var restoredFloor=Restore(floor);Equivalent(floor,restoredFloor,300);
            Assert.That(restoredFloor.World.Colonists[0].SleepsCompleted,Is.EqualTo(1));
        }

        [Test]
        public void HaulCargoAndSharedStorageClaimSurviveRoundTripWithoutDuplicatingWood()
        {
            var s = new SimulationSession(Scenario()); Only(s.World,WorkCategory.Haul); s.World.QueueCreateStorage(P(6,1),P(6,1));
            Until(s,()=>s.World.Colonists[0].CargoAmount>0);
            var carrying = Restore(s); Equivalent(s,carrying,40);
            Until(s,()=>s.World.StorageZones[0].Slots[0].Amount>0 && s.World.StorageZones[0].Slots[0].OwnerId.HasValue);
            var occupiedSlot = Restore(s); var slot = occupiedSlot.World.StorageZones[0].Slots[0];
            Assert.That(slot.Stack.OwnerId,Is.EqualTo(slot.OwnerId)); Assert.That(slot.Stack.Claim,Is.SameAs(slot.Claim));
            Equivalent(s,occupiedSlot,80);
            Assert.That(occupiedSlot.World.ResourceTotal(ResourceKind.Wood,false)+occupiedSlot.World.ResourceTotal(ResourceKind.Wood,true)+occupiedSlot.World.CargoTotal(ResourceKind.Wood),Is.EqualTo(40));
        }

        [Test]
        public void ConstructionDeliveryProgressAndDemolitionResumeWithoutDoubleConsumption()
        {
            var s = new SimulationSession(Scenario());Only(s.World,WorkCategory.Build);s.World.PlaceBlueprint(BuildingKind.Bed,P(4,1));
            Until(s,()=>s.World.Colonists[0].CargoAmount>0);var delivery=Restore(s);Equivalent(s,delivery,15);
            Until(s,()=>s.World.Blueprints[0].Progress>=7);var building=Restore(s);Equivalent(s,building,110);
            var built=s.World.Buildings.Single(b=>b.Exists); Assert.That(built.Kind,Is.EqualTo(BuildingKind.Bed));
            s.World.SetDemolition(built.Id,true);Until(s,()=>s.World.Buildings[0].DemolitionProgress==29);
            var demolition=Restore(s);Equivalent(s,demolition,2);
            Assert.That(demolition.World.ConstructionConsumedWood,Is.EqualTo(10));Assert.That(demolition.World.DemolitionReturnedWood,Is.EqualTo(10));
            Assert.That(demolition.World.Beds.Single(b=>b.Id==built.Id).Exists,Is.False);
        }

        [Test]
        public void CropGrowthAndHarvestBoundaryRoundTripProducesOneHarvest()
        {
            var s=new SimulationSession(Scenario());Only(s.World,WorkCategory.Farm);s.World.CreateGrowingZone(P(2,2),P(2,2));
            Until(s,()=>s.World.Crops[0].Stage==CropStage.Growing);s.World.SetSowingEnabled(s.World.GrowingZones[0].Id,false);s.Step(2399);
            Assert.That(s.World.Crops[0].GrowthTicks,Is.EqualTo(2399));var growing=Restore(s);Equivalent(s,growing,1);
            Until(s,()=>s.World.Crops[0].HarvestProgress==29);var harvest=Restore(s);Equivalent(s,harvest,2);
            Assert.That(harvest.World.ProducedAmount(ResourceKind.Ingredients),Is.EqualTo(6));Assert.That(harvest.World.Crops[0].Stage,Is.EqualTo(CropStage.Empty));
        }

        [Test]
        public void CookingDeliveryAndFinalTickRoundTripKeepInputOutputAndSharedAuthority()
        {
            var s=new SimulationSession(Scenario());Only(s.World,WorkCategory.Build);s.World.PlaceBlueprint(BuildingKind.Stove,P(4,4));
            Until(s,()=>s.World.Buildings.Any(b=>b.Exists&&b.Kind==BuildingKind.Stove));var stove=s.World.Buildings.Single(b=>b.Kind==BuildingKind.Stove);
            Only(s.World,WorkCategory.Cook);s.World.SetCookingOrder(stove.Id,true,7);
            Until(s,()=>s.World.Colonists[0].CargoKind==ResourceKind.Ingredients&&s.World.Colonists[0].CargoAmount>0);var delivery=Restore(s);Equivalent(s,delivery,10);
            Until(s,()=>s.World.CookingOrders[0].Progress==39);var cooking=Restore(s);
            Assert.That(cooking.World.CookingOrders[0].Claim,Is.SameAs(cooking.World.Buildings.Single(b=>b.Id==stove.Id).Claim));
            Equivalent(s,cooking,2);Assert.That(cooking.World.CookingProducedMeals,Is.EqualTo(1));Assert.That(cooking.World.CookingConsumedIngredients,Is.EqualTo(2));
            var final=Restore(cooking);var output=final.World.CookingOutputs.Single(o=>o.Exists);
            Assert.That(output.Stack.Claim,Is.SameAs(output.Claim));Equivalent(cooking,final,10);Assert.That(final.World.CookingProducedMeals,Is.EqualTo(1));
        }

        private static byte[] DataBytes(SaveData data)
        {
            using(var stream=new MemoryStream())
            {
                new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(SaveData)).WriteObject(stream,data);
                return stream.ToArray();
            }
        }
        private static int Total(WorldState world,ResourceKind kind)
            => world.ResourceTotal(kind,false)+world.ResourceTotal(kind,true)+world.CargoTotal(kind)
                +(kind==ResourceKind.Wood?world.ConstructionWood:kind==ResourceKind.Ingredients?world.CookingOrders.Where(o=>o.Exists).Sum(o=>o.InputIngredients):0);

        [Test]
        public void InvalidWorkAndLifeRoutesCancelOnlyRestoredCandidatesAndDropCargoOnceThroughBothFiles()
        {
            foreach(var activity in new[]{"haul","build","cook","farm","gather","sleep","food"})
            foreach(var damage in new[]{"discontinuous","blocked"})
            {
                var source=new SimulationSession(Scenario());var world=source.World;
                switch(activity)
                {
                    case "haul": Only(world,WorkCategory.Haul);world.QueueCreateStorage(P(6,1),P(6,1));break;
                    case "build": Only(world,WorkCategory.Build);world.PlaceBlueprint(BuildingKind.Bed,P(4,1));break;
                    case "cook":
                        Only(world,WorkCategory.Build);world.PlaceBlueprint(BuildingKind.Stove,P(4,4));
                        Until(source,()=>world.Buildings.Any(b=>b.Exists&&b.Kind==BuildingKind.Stove));
                        Only(world,WorkCategory.Cook);world.SetCookingOrder(world.Buildings.Single(b=>b.Exists&&b.Kind==BuildingKind.Stove).Id,true,7);break;
                    case "farm": Only(world,WorkCategory.Farm);world.CreateGrowingZone(P(6,1),P(6,1));break;
                    case "gather": Only(world,WorkCategory.Gather);world.QueueDesignateGather(301,true);break;
                    case "sleep":
                        source=new SimulationSession(new ScenarioDefinition(8,8,Array.Empty<GridPosition>(),
                            new[]{new ColonistSeed(1,P(1,1),9000,2000)},Array.Empty<ResourceSeed>(),new[]{new BedSeed(201,P(6,1))},true));break;
                    case "food":
                        source=new SimulationSession(new ScenarioDefinition(8,8,Array.Empty<GridPosition>(),
                            new[]{new ColonistSeed(1,P(1,1),3000,9000)},new[]{new ResourceSeed(101,ResourceKind.Meal,6,P(6,1))},Array.Empty<BedSeed>(),true));break;
                }
                world=source.World;
                var carries=activity=="haul"||activity=="build"||activity=="cook";
                Until(source,()=>world.Colonists[0].Status==MovementStatus.Moving&&(!carries||world.Colonists[0].CargoAmount>0));
                var original=State(world);var capture=source.CaptureSaveData(true,3);
                Assert.That(capture.Success,Is.True,capture.ErrorCode);
                var pawnData=capture.Data.World.Colonists[0];
                Assert.That(pawnData.Path.Count,Is.GreaterThan(0));
                if(damage=="blocked")
                {
                    var next=pawnData.Path[0];
                    capture.Data.World.BlockedTiles.Add(new PositionSaveData{X=next.X,Y=next.Y});
                }
                else pawnData.Path[0]=new PositionSaveData{X=7,Y=7};
                var inputBytes=DataBytes(capture.Data);
                var root=Path.GetFullPath(Path.Combine(Application.dataPath,"..","TestResults","InvalidSaveRoutes"));
                var directory=Path.GetFullPath(Path.Combine(root,Guid.NewGuid().ToString("N")));
                try
                {
                    var file=new SaveFileService();var legacy=Path.Combine(directory,"settlement-v1.json");
                    var saved=file.Save(legacy,capture.Data);Assert.That(saved.Success,Is.True,activity+" "+damage+" "+saved.ErrorCode);
                    var repository=new SaveRepository(directory);var slot=repository.SaveNew(activity+" "+damage,capture.Data);
                    Assert.That(slot.Success,Is.True,slot.ErrorCode);
                    var slotPath=Path.Combine(directory,slot.Slot.SlotId+".save.json");
                    var legacyBytes=File.ReadAllBytes(legacy);var slotBytes=File.ReadAllBytes(slotPath);
                    var legacyLoad=file.Load(legacy);var slotLoad=new SaveRepository(directory).Load(slot.Slot.SlotId);
                    Assert.That(legacyLoad.Success,Is.True,legacyLoad.ErrorCode);Assert.That(slotLoad.Success,Is.True,slotLoad.ErrorCode);
                    SimulationSession first=null;
                    foreach(var data in new[]{legacyLoad.Data,slotLoad.Data})
                    {
                        Assert.That(SimulationSession.TryRestoreSave(data,out var restored,out var error),Is.True,error);
                        var pawn=restored.World.Colonists[0];
                        Assert.That(pawn.IsWorking,Is.False);Assert.That(pawn.IsLifeBusy,Is.False);
                        Assert.That(pawn.WorkReason,Is.EqualTo("RestoreInvalid"));Assert.That(pawn.CargoAmount,Is.Zero);
                        Assert.That(pawn.Path,Is.Empty);Assert.That(pawn.MovementProgress,Is.Zero);
                        Assert.That(restored.World.Resources.All(r=>!r.OwnerId.HasValue&&r.Reservations.Count==0),Is.True);
                        Assert.That(restored.World.Beds.All(b=>!b.OwnerId.HasValue&&!b.IsOccupied),Is.True);
                        Assert.That(restored.World.StorageZones.SelectMany(z=>z.Slots).All(s=>!s.OwnerId.HasValue),Is.True);
                        Assert.That(restored.World.Blueprints.All(b=>!b.OwnerId.HasValue),Is.True);
                        Assert.That(restored.World.Buildings.All(b=>!b.OwnerId.HasValue),Is.True);
                        Assert.That(restored.World.Crops.All(c=>!c.OwnerId.HasValue),Is.True);
                        Assert.That(restored.World.Gatherables.All(g=>!g.OwnerId.HasValue),Is.True);
                        foreach(ResourceKind kind in Enum.GetValues(typeof(ResourceKind)))Assert.That(Total(restored.World,kind),Is.EqualTo(Total(world,kind)));
                        var cleaned=restored.CaptureSaveData(true,3);Assert.That(cleaned.Success,Is.True,cleaned.ErrorCode);
                        Assert.That(SimulationSession.TryRestoreSave(cleaned.Data,out var twice,out error),Is.True,error);
                        CollectionAssert.AreEqual(DataBytes(cleaned.Data),DataBytes(twice.CaptureSaveData(true,3).Data),"Restoring a cleaned candidate must not drop again or allocate another ID.");
                        restored.World.SetDrafted(1,true);
                        if(first==null)first=restored;
                        else
                        {
                            Equivalent(first,restored,50);
                            foreach(ResourceKind kind in Enum.GetValues(typeof(ResourceKind)))Assert.That(Total(restored.World,kind),Is.EqualTo(Total(world,kind)));
                        }
                    }
                    CollectionAssert.AreEqual(inputBytes,DataBytes(capture.Data));Assert.That(State(world),Is.EqualTo(original));
                    CollectionAssert.AreEqual(legacyBytes,File.ReadAllBytes(legacy));CollectionAssert.AreEqual(slotBytes,File.ReadAllBytes(slotPath));
                }
                finally
                {
                    Assert.That(directory.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);
                    if(Directory.Exists(directory))Directory.Delete(directory,true);
                }
            }
        }

        [Test]
        public void CapturedPoliciesAreIndependentAndResetUsesOriginalScenario()
        {
            var s=new SimulationSession(Scenario());s.Reset();s.World.SetWorkPriority(1,WorkCategory.Cook,0);s.World.QueueCreateStorage(P(6,1),P(6,1));
            s.World.QueueStorageFilter(s.World.StorageZones[0].Id,ResourceKind.Wood,false);s.World.CreateGrowingZone(P(2,2),P(2,2));s.World.SetSowingEnabled(s.World.GrowingZones[0].Id,false);
            var capture=s.CaptureSaveData();Assert.That(capture.Success,Is.True);s.World.SetWorkPriority(1,WorkCategory.Cook,3);
            Assert.That(SimulationSession.TryRestoreSave(capture.Data,out var restored,out var error),Is.True,error);
            Assert.That(restored.World.WorkPolicies[0].GetPriority(WorkCategory.Cook),Is.Zero);Assert.That(restored.World.StorageZones[0].Allows(ResourceKind.Wood),Is.False);
            Assert.That(restored.World.GrowingZones[0].SowingEnabled,Is.False);Assert.That(restored.ResetCount,Is.EqualTo(1));
            restored.Reset();Assert.That(restored.ResetCount,Is.EqualTo(2));Assert.That(restored.World.StorageZones.Count,Is.Zero);Assert.That(restored.World.Crops.Count,Is.Zero);
            Assert.That(State(restored.World),Is.EqualTo(State(new SimulationSession(Scenario()).World)));
        }

        [Test]
        public void CorruptIdentityClaimVersionAndTimingRejectWithoutTouchingSource()
        {
            var s = new SimulationSession(Scenario(true,3000,9000)); s.Step(8); var before=State(s.World);
            var corruptions = new Action<SaveData>[] {
                d=>d.SchemaVersion=999,
                d=>d.World.Resources[0].Id=d.World.Colonists[0].Id,
                d=>d.World.Resources.Single(r=>r.Id==103).Claim.OwnerId=999,
                d=>d.World.Resources.Single(r=>r.Id==103).Claim.ActionId++,
                d=>d.World.NextEntityId=1,
                d=>d.Session.PendingSeconds="0.1",
                d=>d.Session.Speed=2,
                d=>d.World.Colonists[0].ActionDuration=0,
                d=>d.World.Resources[0].Amount++ };
            foreach(var corrupt in corruptions)
            {
                var capture=s.CaptureSaveData();Assert.That(capture.Success,Is.True);corrupt(capture.Data);
                Assert.That(SimulationSession.TryRestoreSave(capture.Data,out var restored,out var error),Is.False);
                Assert.That(restored,Is.Null);Assert.That(error,Does.StartWith("Save"));Assert.That(State(s.World),Is.EqualTo(before));
            }
        }

        [Test]
        public void ImpossibleIdleCargoFeatureStageAndFacilityTopologyRejectRestore()
        {
            var idle=new SimulationSession(Scenario());var idleData=idle.CaptureSaveData().Data;
            idleData.World.Colonists[0].CargoAmount=1;idleData.World.Colonists[0].CargoKind=nameof(ResourceKind.Wood);
            Assert.That(SimulationSession.TryRestoreSave(idleData,out var invalidIdle,out _),Is.False);
            Assert.That(invalidIdle,Is.Null);Assert.That(idle.World.Colonists[0].CargoAmount,Is.Zero);

            var s=new SimulationSession(Scenario());Only(s.World,WorkCategory.Build);s.World.PlaceBlueprint(BuildingKind.Stove,P(4,4));
            Until(s,()=>s.World.Buildings.Any(b=>b.Exists&&b.Kind==BuildingKind.Stove));var stove=s.World.Buildings.Single(b=>b.Exists&&b.Kind==BuildingKind.Stove);
            var before=State(s.World);var topology=s.CaptureSaveData().Data;
            Assert.That(topology.World.BlockedTiles.RemoveAll(p=>p.X==stove.Position.X&&p.Y==stove.Position.Y),Is.EqualTo(1));
            Assert.That(SimulationSession.TryRestoreSave(topology,out var invalidTopology,out _),Is.False);
            Assert.That(invalidTopology,Is.Null);Assert.That(State(s.World),Is.EqualTo(before));

            Only(s.World,WorkCategory.Cook);s.World.SetCookingOrder(stove.Id,true,7);Until(s,()=>s.World.CookingOrders[0].Progress>0);
            before=State(s.World);var wrongStage=s.CaptureSaveData().Data;
            Assert.That(wrongStage.World.Colonists[0].FeatureStep,Is.EqualTo(nameof(WorkStep.Cook)));
            wrongStage.World.Colonists[0].WorkActivity=nameof(WorkActivity.GoingToGather);
            Assert.That(SimulationSession.TryRestoreSave(wrongStage,out var invalidStage,out _),Is.False);
            Assert.That(invalidStage,Is.Null);Assert.That(State(s.World),Is.EqualTo(before));
        }

        [Test]
        public void UnsafePendingCommandsRejectCaptureWithoutAdvancingOrMutatingWorld()
        {
            var s=new SimulationSession(Scenario());s.World.QueueSetBlocked(P(7,7),true);var before=State(s.World);
            var blocked=s.CaptureSaveData();Assert.That(blocked.Success,Is.False);Assert.That(blocked.ErrorCode,Is.EqualTo("SavePendingCommands"));Assert.That(State(s.World),Is.EqualTo(before));
            s.Step(1);s.World.QueueSetResourceAmount(101,50);before=State(s.World);
            var resource=s.CaptureSaveData();Assert.That(resource.Success,Is.False);Assert.That(resource.ErrorCode,Is.EqualTo("SavePendingCommands"));Assert.That(State(s.World),Is.EqualTo(before));
        }
    }
}
