using System;
using System.Linq;
using NUnit.Framework;
using RimworldTweak.Simulation;

namespace RimworldTweak.Tests.EditMode
{
    public sealed class PriorityWorkTests
    {
        private static GridPosition P(int x, int y) => new GridPosition(x, y);

        private static SimulationSession Game(int wood = 20, bool needs = false, int nutrition = 10000, bool isolateTree = false)
        {
            var blocked = isolateTree ? new[] {P(4, 5), P(6, 5), P(5, 4), P(5, 6)} : Array.Empty<GridPosition>();
            return new SimulationSession(new ScenarioDefinition(8, 8, blocked,
                new[] {new ColonistSeed(1, P(1, 1), nutrition), new ColonistSeed(2, P(1, 2))},
                wood > 0 ? new[] {new ResourceSeed(10, ResourceKind.Wood, wood, P(2, 1))} : Array.Empty<ResourceSeed>(),
                new[] {new BedSeed(20, P(4, 2))}, needs, new[] {new GatherSeed(30, GatherKind.Tree, P(5, 5))}));
        }

        [Test]
        public void PrioritySelectsOneWorkerAndRejectionPreservesCurrentJob()
        {
            var w = Game(0).World;
            w.QueueDesignateGather(30, true);
            w.PlaceBlueprint(BuildingKind.Bed, P(4, 3));
            Assert.That(w.PrioritizeWork(2, 30), Is.True);
            var worker = w.Colonists[1];
            var action = worker.ActionId;
            Assert.That(w.PrioritizeWork(1, 30), Is.False);
            Assert.That(w.LastCommandError, Is.EqualTo("TargetReserved"));
            Assert.That(w.PrioritizeWork(2, w.Blueprints[0].Id), Is.False);
            Assert.That(w.LastCommandError, Is.EqualTo("MissingMaterials"));
            Assert.That(worker.ActionId, Is.EqualTo(action));
            Assert.That(w.Gatherables[0].OwnerId, Is.EqualTo(2));
            Assert.That(w.PriorityWorkTarget(2), Is.EqualTo(30));
            Assert.That(w.WorkPolicies[0].GetPriority(WorkCategory.Gather), Is.EqualTo(2));
            Assert.That(w.CancelPriorityWork(2), Is.True);
            Assert.That(w.Gatherables[0].Designated, Is.True);
            Assert.That(w.PrioritizeWork(1, 30), Is.True);
            w.Step(200);
            Assert.That(w.Gatherables[0].Depleted, Is.True);
            Assert.That(w.PriorityWorkTarget(1), Is.Null);
        }

        [Test]
        public void DeliveryLocksSiteBeforePickupAndRestoresThroughConstruction()
        {
            var session = Game();
            var w = session.World;
            Assert.That(w.PlaceBlueprint(BuildingKind.Stove, P(5, 3)), Is.True);
            var plan = w.Blueprints.Single();
            Assert.That(w.PrioritizeWork(2, plan.Id), Is.True);
            Assert.That(w.PrioritizeWork(1, plan.Id), Is.False);
            Assert.That(w.LastCommandError, Is.EqualTo("TargetReserved"));
            Assert.That(plan.OwnerId, Is.Null);
            var saved = session.CaptureSaveData();
            Assert.That(saved.Success, Is.True, saved.ErrorCode);
            Assert.That(SimulationSession.TryRestoreSave(saved.Data, out var restored, out var error), Is.True, error);
            foreach (var world in new[] {w, restored.World})
            {
                Assert.That(world.PriorityWorkTarget(2), Is.EqualTo(plan.Id));
                for (var i=0; i<500 && world.Blueprints[0].Exists; i++)
                {
                    Assert.That(world.PriorityWorkTarget(2), Is.EqualTo(plan.Id));
                    world.Step(1);
                }
                Assert.That(world.Blueprints[0].Exists, Is.False);
                Assert.That(world.PriorityWorkTarget(2), Is.Null);
                Assert.That(world.ConstructionConsumedWood, Is.EqualTo(15));
            }
            Assert.That(w.Tick, Is.EqualTo(restored.World.Tick));
        }

        [Test]
        public void RedirectCarriedWoodAndDraftDoNotLoseOrDuplicateIt()
        {
            var w = Game(5).World;
            w.SetWorkPriority(2, WorkCategory.Haul, 0);
            w.QueueCreateStorage(P(6, 6), P(6, 6));
            var pawn = w.Colonists[0];
            for (var i=0; i<80 && pawn.CargoAmount==0; i++) w.Step(1);
            Assert.That(pawn.CargoAmount, Is.EqualTo(5));
            Assert.That(w.PlaceBlueprint(BuildingKind.Door, P(4, 3)), Is.True);
            Assert.That(w.PrioritizeWork(1, w.Blueprints[0].Id), Is.True, w.LastCommandError);
            w.SetDrafted(1, true);
            Assert.That(w.PriorityWorkTarget(1), Is.Null);
            Assert.That(w.ResourceTotal(ResourceKind.Wood, false) + w.ResourceTotal(ResourceKind.Wood, true) +
                w.CargoTotal(ResourceKind.Wood) + w.ConstructionWood, Is.EqualTo(5));
            Assert.That(w.PrioritizeWork(2, w.Blueprints[0].Id), Is.True);
        }

        [Test]
        public void PersonalWorkHonorsBlockedRoutesDutyNeedsAndRepairDesignation()
        {
            var w = Game(isolateTree:true).World;
            w.QueueDesignateGather(30, true);
            Assert.That(w.PrioritizeWork(1, 30), Is.False);
            Assert.That(w.LastCommandError, Is.EqualTo("Unreachable"));
            w.SetWorkPriority(1, WorkCategory.Gather, 0);
            Assert.That(w.PrioritizeWork(1, 30), Is.False);
            Assert.That(w.LastCommandError, Is.EqualTo("Disabled"));
            var hungry=Game(needs:true,nutrition:1000).World;
            hungry.QueueDesignateGather(30,true);
            Assert.That(hungry.PrioritizeWork(1,30),Is.False);
            Assert.That(hungry.LastCommandError,Is.EqualTo("NeedsPriority"));
            var bed = w.Buildings.Single(b => b.Id == 20);
            w.ApplyCombatDamage(20, 45);
            Assert.That(w.PrioritizeWork(1,20),Is.False);
            Assert.That(w.SetRepair(20,true),Is.True);
            Assert.That(w.PrioritizeWork(1,20),Is.True,w.LastCommandError);
            for (var i=0; i<300 && bed.Durability<100; i++)
            {
                Assert.That(w.PriorityWorkTarget(1),Is.EqualTo(20));
                w.Step(1);
            }
            Assert.That(bed.Durability,Is.EqualTo(100));
            Assert.That(w.PriorityWorkTarget(1),Is.Null);
            Assert.That(w.RepairWoodConsumed,Is.EqualTo(4));
        }
    }
}
