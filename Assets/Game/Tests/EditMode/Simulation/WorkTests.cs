using System;
using System.Linq;
using NUnit.Framework;
using RimworldTweak.Simulation;

namespace RimworldTweak.Tests.EditMode
{
    public sealed class WorkTests
    {
        private static GridPosition P(int x, int y) => new GridPosition(x, y);
        private static WorldState World(ColonistSeed[] pawns = null, ResourceSeed[] resources = null,
            GatherSeed[] nodes = null, bool needs = false, BedSeed[] beds = null)
            => WorldState.Create(new ScenarioDefinition(8, 8, Array.Empty<GridPosition>(),
                pawns ?? new[] { new ColonistSeed(1, P(1, 1)) }, resources ?? Array.Empty<ResourceSeed>(), beds, needs, nodes));
        private static void Until(WorldState w, Func<bool> condition, int limit = 500)
        {
            for (var i = 0; i < limit && !condition(); i++) w.Step(1);
            Assert.That(condition(), Is.True, "Bounded expected transition failed at tick " + w.Tick);
        }
        private static int Total(WorldState w, ResourceKind kind) => w.ResourceTotal(kind, false) + w.ResourceTotal(kind, true) + w.CargoTotal(kind);
        private static void Unowned(WorldState w)
        {
            Assert.That(w.Resources.All(r => !r.OwnerId.HasValue), Is.True);
            Assert.That(w.Gatherables.All(r => !r.OwnerId.HasValue), Is.True);
            Assert.That(w.StorageZones.SelectMany(z => z.Slots).All(r => !r.OwnerId.HasValue), Is.True);
        }

        [Test]
        public void GatherCompletesExactlyOnceAtDurationAndStartsAfterArrival()
        {
            foreach (var kind in new[] { GatherKind.Tree, GatherKind.EdiblePlant })
            {
                var w = World(nodes: new[] { new GatherSeed(101, kind, P(1, 1)) });
                var node = w.Gatherables[0]; w.QueueDesignateGather(101, true); w.Step(1);
                Assert.That(node.Progress, Is.Zero); Assert.That(node.OwnerId, Is.EqualTo(1));
                w.Step(node.Duration - 1); Assert.That(Total(w, node.ResourceKind), Is.Zero);
                w.Step(1); Assert.That(Total(w, node.ResourceKind), Is.EqualTo(node.Yield));
                Assert.That(node.Depleted, Is.True); w.Step(100);
                Assert.That(Total(w, node.ResourceKind), Is.EqualTo(node.Yield)); Unowned(w);
            }
        }

        [Test]
        public void GatherDraftTransfersWorkButPreservesDesignationAndProgress()
        {
            var w = World(new[] { new ColonistSeed(1, P(1, 1)), new ColonistSeed(2, P(1, 1)) },
                nodes: new[] { new GatherSeed(101, GatherKind.Tree, P(1, 1)) });
            w.QueueDesignateGather(101, true); w.Step(21);
            Assert.That(w.Gatherables[0].Progress, Is.EqualTo(20));
            var tick = w.Tick; w.ToggleDraft(1);
            Assert.That(w.Tick, Is.EqualTo(tick)); Assert.That(w.Gatherables[0].OwnerId, Is.Null);
            w.Step(1); Assert.That(w.Gatherables[0].OwnerId, Is.EqualTo(2));
            w.Step(40); Assert.That(Total(w, ResourceKind.Wood), Is.EqualTo(10));
        }

        [Test]
        public void DesignationCancelAtCompletionBoundaryKeepsProgressWithoutYield()
        {
            var w = World(nodes: new[] { new GatherSeed(101, GatherKind.Tree, P(1, 1)) });
            w.QueueDesignateGather(101, true); w.Step(60);
            w.QueueDesignateGather(101, false); w.Step(1);
            Assert.That(w.Gatherables[0].Progress, Is.EqualTo(59)); Assert.That(Total(w, ResourceKind.Wood), Is.Zero); Unowned(w);
            w.QueueDesignateGather(101, true); w.Step(2); Assert.That(Total(w, ResourceKind.Wood), Is.EqualTo(10));
        }

        [Test]
        public void StorageRejectsInvalidAreasAndAppliesCommandsInOrder()
        {
            var w = World(beds: new[] { new BedSeed(201, P(5, 5)) });
            Assert.That(w.QueueCreateStorage(P(-1, 0), P(0, 0)), Is.False);
            Assert.That(w.QueueCreateStorage(P(5, 5), P(6, 6)), Is.False);
            Assert.That(w.QueueCreateStorage(P(2, 2), P(3, 3)), Is.True);
            Assert.That(w.QueueCreateStorage(P(2, 2), P(3, 3)), Is.False);
            Assert.That(w.PendingCommandCount, Is.Zero); Assert.That(w.Tick, Is.Zero);
            Assert.That(w.StorageZones.Count, Is.EqualTo(1));
            Assert.That(w.StorageZones[0].Slots.Count, Is.EqualTo(4));
            Assert.That(w.LastCommandError, Is.EqualTo("InvalidStorageArea"));
        }

        [Test]
        public void PartialCapacityTransfersOnlyFiveAndDoesNotCountAsConsumption()
        {
            var w = World(resources: new[] { new ResourceSeed(101, ResourceKind.Wood, 63, P(1, 1)) });
            w.QueueCreateStorage(P(2, 1), P(2, 1));
            Until(w, () => w.ResourceTotal(ResourceKind.Wood, true) == 40);
            // Fill to 45 via the external test hook, then expect one five-unit transfer.
            var stack = w.StorageZones[0].Slots[0].Stack;
            w.QueueSetResourceAmount(stack.Id, 45);
            Until(w, () => w.Colonists[0].CargoAmount == 5);
            Assert.That(w.ResourceTotal(ResourceKind.Wood, true), Is.EqualTo(45));
            Assert.That(w.ResourceTotal(ResourceKind.Wood, false), Is.EqualTo(18));
            Until(w, () => w.ResourceTotal(ResourceKind.Wood, true) == 50);
            Assert.That(Total(w, ResourceKind.Wood), Is.EqualTo(68));
            Assert.That(w.Resources.Sum(r => r.ConsumedAmount), Is.Zero);
            Assert.That(w.Resources.Sum(r => r.AddedAmount), Is.EqualTo(5));
            w.Step(20); Assert.That(w.Colonists[0].WorkReason, Is.EqualTo("StorageFull")); Unowned(w);
        }

        [Test]
        public void DestinationCompetitionNeverPicksUpWithoutClaim()
        {
            var w = World(new[] { new ColonistSeed(1, P(1, 1)), new ColonistSeed(2, P(1, 1)) },
                new[] { new ResourceSeed(101, ResourceKind.Wood, 10, P(1, 1)), new ResourceSeed(102, ResourceKind.Wood, 10, P(1, 1)) });
            w.QueueCreateStorage(P(6, 1), P(6, 1)); w.Step(2);
            Assert.That(w.Colonists.Count(c => c.CargoAmount > 0), Is.EqualTo(1));
            Assert.That(w.ResourceTotal(ResourceKind.Wood, false), Is.EqualTo(10));
            Assert.That(Total(w, ResourceKind.Wood), Is.EqualTo(20));
            Until(w, () => w.ResourceTotal(ResourceKind.Wood, true) == 20); Unowned(w);
        }

        [Test]
        public void DraftAtEveryHaulStageConservesAndStaleCancelCannotAffectNewAction()
        {
            foreach (var stage in new[] { WorkActivity.GoingToPickup, WorkActivity.PickingUp, WorkActivity.GoingToStore, WorkActivity.Storing })
            {
                var w = World(resources: new[] { new ResourceSeed(101, ResourceKind.Wood, 10, P(3, 1)) });
                var pawn = w.Colonists[0]; w.QueueCreateStorage(P(6, 1), P(6, 1));
                Until(w, () => pawn.WorkActivity == stage); var old = pawn.ActionId;
                w.SetDrafted(1, true); Assert.That(pawn.CargoAmount, Is.Zero); Unowned(w);
                Assert.That(Total(w, ResourceKind.Wood), Is.EqualTo(10));
                w.SetDrafted(1, false); w.Step(1); var action = pawn.ActionId;
                w.QueueCancelLife(1, old); w.Step(1); Assert.That(pawn.ActionId, Is.EqualTo(action));
                Until(w, () => w.ResourceTotal(ResourceKind.Wood, true) == 10);
                Assert.That(Total(w, ResourceKind.Wood), Is.EqualTo(10));
            }
        }

        [Test]
        public void FilterChangeAndZoneDeletionKeepAllMaterials()
        {
            var w = World(resources: new[] { new ResourceSeed(101, ResourceKind.Wood, 18, P(1, 1)) });
            w.QueueCreateStorage(P(6, 1), P(6, 1)); Until(w, () => w.CargoTotal(ResourceKind.Wood) == 10);
            var zone = w.StorageZones[0]; w.QueueStorageFilter(zone.Id, ResourceKind.Wood, false); w.Step(1);
            Assert.That(w.CargoTotal(ResourceKind.Wood), Is.Zero); Assert.That(Total(w, ResourceKind.Wood), Is.EqualTo(18)); Unowned(w);
            w.QueueStorageFilter(zone.Id, ResourceKind.Wood, true);
            Until(w, () => w.ResourceTotal(ResourceKind.Wood, true) == 18);
            w.QueueDeleteStorage(zone.Id); w.Step(1);
            Assert.That(w.ResourceTotal(ResourceKind.Wood, false), Is.EqualTo(18));
            Assert.That(w.ResourceTotal(ResourceKind.Wood, true), Is.Zero); Unowned(w);
        }

        [Test]
        public void HungryWorkerCanProduceFoodWithoutRepeatedCancellation()
        {
            var w = World(new[] { new ColonistSeed(1, P(1, 1), 2000, 9000) },
                nodes: new[] { new GatherSeed(101, GatherKind.EdiblePlant, P(1, 1)) }, needs: true);
            w.QueueDesignateGather(101, true); w.Step(31);
            Assert.That(w.ProducedAmount(ResourceKind.Ingredients), Is.EqualTo(4));
            Until(w, () => w.Colonists[0].RawMealsEaten == 1);
            Assert.That(Total(w, ResourceKind.Ingredients), Is.Zero);
            Assert.That(w.Resources.Sum(r => r.ConsumedAmount), Is.EqualTo(4));
        }

        [Test]
        public void HungerDuringFoodHaulDropsThenUsesExistingEatingRules()
        {
            var w = World(new[] { new ColonistSeed(1, P(1, 1), 3040, 9000) },
                new[] { new ResourceSeed(101, ResourceKind.Meal, 10, P(1, 1)) }, needs: true);
            w.QueueCreateStorage(P(7, 7), P(7, 7)); w.Step(2);
            Assert.That(w.Colonists[0].CargoAmount, Is.EqualTo(10));
            w.Step(2); Assert.That(w.Colonists[0].CargoAmount, Is.Zero);
            Assert.That(w.Colonists[0].LifeActivity, Is.EqualTo(LifeActivity.Eating));
            Assert.That(w.StorageZones[0].Slots[0].OwnerId, Is.Null);
            Until(w, () => w.Colonists[0].MealsEaten == 1);
            Assert.That(Total(w, ResourceKind.Meal), Is.EqualTo(9));
        }

        [Test]
        public void SleepInterruptsWorkAndKeepsSleepIntentUntilRestored()
        {
            var w = World(new[] { new ColonistSeed(1, P(1, 1), 9000, 2020) },
                nodes: new[] { new GatherSeed(101, GatherKind.Tree, P(1, 1)) }, needs: true);
            w.QueueDesignateGather(101, true); w.Step(4);
            Assert.That(w.Colonists[0].LifeActivity, Is.EqualTo(LifeActivity.SleepingOnFloor));
            Assert.That(w.Gatherables[0].OwnerId, Is.Null);
            w.Step(20); Assert.That(w.Colonists[0].Rest, Is.GreaterThan(2000));
            Assert.That(w.Colonists[0].IsWorking, Is.False);
        }

        [Test]
        public void StoredFoodAndIncomingCargoShareOneAuthority()
        {
            var w = World(new[] { new ColonistSeed(1, P(1, 1), 9000, 9000), new ColonistSeed(2, P(2, 1), 3500, 9000) },
                new[] { new ResourceSeed(101, ResourceKind.Meal, 25, P(1, 1)) }, needs: true);
            w.SetDrafted(2, true); w.QueueCreateStorage(P(2, 1), P(2, 1));
            Until(w, () => w.ResourceTotal(ResourceKind.Meal, true) == 25);
            w.SetDrafted(1, true); w.Step(50); w.SetDrafted(2, false); w.Step(1);
            var slot = w.StorageZones[0].Slots[0];
            Assert.That(slot.OwnerId, Is.EqualTo(2)); Assert.That(slot.Stack.OwnerId, Is.EqualTo(slot.OwnerId));
            Assert.That(slot.Stack.AccessibleAmount, Is.Zero);
            w.SetDrafted(2, true); Assert.That(slot.OwnerId, Is.Null); Assert.That(slot.Stack.OwnerId, Is.Null);
        }

        [Test]
        public void ThreePawnsGatherAndStoreFiniteResourcesWithoutPingPong()
        {
            var nodes = new[] { new GatherSeed(101, GatherKind.Tree, P(1, 1)), new GatherSeed(102, GatherKind.Tree, P(2, 1)),
                new GatherSeed(103, GatherKind.EdiblePlant, P(3, 1)), new GatherSeed(104, GatherKind.EdiblePlant, P(4, 1)) };
            var w = World(new[] { new ColonistSeed(1, P(1, 1)), new ColonistSeed(2, P(2, 1)), new ColonistSeed(3, P(3, 1)) }, nodes: nodes, needs: true);
            w.QueueCreateStorage(P(1, 3), P(4, 3)); foreach (var n in nodes) w.QueueDesignateGather(n.Id, true);
            w.Step(300);
            Assert.That(w.ResourceTotal(ResourceKind.Wood, true), Is.EqualTo(20));
            Assert.That(w.ResourceTotal(ResourceKind.Ingredients, true), Is.EqualTo(8));
            Assert.That(w.Colonists.All(c => c.CargoAmount == 0 && !c.IsWorking), Is.True);
            Assert.That(w.Gatherables.All(n => n.Depleted && !n.Designated), Is.True); Unowned(w);
            var searches = w.PathSearchCount; w.Step(5); Assert.That(w.PathSearchCount - searches, Is.LessThan(10));
        }

        [Test]
        public void BlockedHaulDropsAndResumesAfterCorridorOpens()
        {
            var w = World(resources: new[] { new ResourceSeed(101, ResourceKind.Wood, 10, P(1, 1)) });
            w.QueueCreateStorage(P(6, 1), P(6, 1)); Until(w, () => w.Colonists[0].CargoAmount == 10);
            for (var y = 0; y < 8; y++) w.QueueSetBlocked(P(4, y), true);
            w.Step(1); Assert.That(w.Colonists[0].CargoAmount, Is.Zero); Unowned(w);
            Assert.That(Total(w, ResourceKind.Wood), Is.EqualTo(10));
            w.Step(30); Assert.That(w.ResourceTotal(ResourceKind.Wood, true), Is.Zero);
            w.QueueSetBlocked(P(4, 1), false); Until(w, () => w.ResourceTotal(ResourceKind.Wood, true) == 10);
            Assert.That(Total(w, ResourceKind.Wood), Is.EqualTo(10));
        }

        [Test]
        public void LifeSelectionWinsSameTickAsIncomingStackClaim()
        {
            // Store ten meals, then arrange a second source and a hungry pawn on the pickup boundary.
            var w = World(new[] { new ColonistSeed(1, P(1, 1), 10000, 10000), new ColonistSeed(2, P(2, 1), 3200, 10000) },
                new[] { new ResourceSeed(101, ResourceKind.Meal, 10, P(1, 1)), new ResourceSeed(102, ResourceKind.Meal, 10, P(6, 1)) }, needs: true);
            w.SetDrafted(2, true); w.QueueCreateStorage(P(2, 1), P(2, 1));
            Until(w, () => w.ResourceTotal(ResourceKind.Meal, true) == 10);
            Until(w, () => w.Colonists[0].WorkActivity == WorkActivity.PickingUp);
            w.SetDrafted(2, false); w.Step(1);
            var slot = w.StorageZones[0].Slots[0];
            Assert.That(slot.OwnerId, Is.EqualTo(2));
            Assert.That(w.Colonists[1].LifeActivity, Is.EqualTo(LifeActivity.Eating));
            Assert.That(w.Colonists[0].CargoAmount, Is.Zero);
            Assert.That(Total(w, ResourceKind.Meal), Is.EqualTo(20));
        }

        [Test]
        public void WorkScenarioResetAndEqualTickBatchesProduceEquivalentState()
        {
            var scenario = WorkScenario.Create(); var a = new SimulationSession(scenario); var b = new SimulationSession(scenario);
            foreach (var session in new[] { a, b })
            {
                session.World.QueueCreateStorage(P(0, 6), P(0, 7));
                foreach (var node in session.World.Gatherables) session.World.QueueDesignateGather(node.Id, true);
            }
            for (var i = 0; i < 300; i++) a.Step(1);
            for (var i = 0; i < 100; i++) b.Step(3);
            Assert.That(a.World.Colonists.Select(c => (c.Position, c.ActionId, c.WorkActivity, c.LifeActivity, c.CargoAmount)),
                Is.EqualTo(b.World.Colonists.Select(c => (c.Position, c.ActionId, c.WorkActivity, c.LifeActivity, c.CargoAmount))));
            Assert.That(a.World.Resources.Select(r => (r.Id, r.Position, r.Kind, r.Amount, r.StorageSlotId)),
                Is.EqualTo(b.World.Resources.Select(r => (r.Id, r.Position, r.Kind, r.Amount, r.StorageSlotId))));
            for (var i = 0; i < 10; i++)
            {
                var old = a.World; a.Reset();
                Assert.That(a.World, Is.Not.SameAs(old)); Assert.That(a.World.Tick, Is.Zero);
                Assert.That(a.World.StorageZones, Is.Empty); Assert.That(a.World.Resources.Count, Is.EqualTo(2));
                Assert.That(a.World.Gatherables.All(n => !n.Designated && n.Progress == 0), Is.True); Unowned(a.World);
            }
        }
    }
}
