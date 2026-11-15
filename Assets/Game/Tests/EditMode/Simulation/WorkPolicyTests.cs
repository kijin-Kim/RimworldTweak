using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RimworldTweak.Simulation;

namespace RimworldTweak.Tests.EditMode
{
    public sealed class WorkPolicyTests
    {
        private static GridPosition P(int x, int y) => new GridPosition(x, y);
        private static WorldState World(bool two = false, bool needs = false, int nutrition = 10000)
            => WorldState.Create(new ScenarioDefinition(8, 8, Array.Empty<GridPosition>(),
                two ? new[] { new ColonistSeed(1, P(1, 1)), new ColonistSeed(2, P(1, 1)) } : new[] { new ColonistSeed(1, P(1, 1), nutrition, 10000) },
                new[] { new ResourceSeed(101, ResourceKind.Wood, 20, P(1, 1)), new ResourceSeed(102, ResourceKind.Meal, 10, P(1, 2)) },
                needsEnabled: needs, gatherables: new[] { new GatherSeed(201, GatherKind.Tree, P(2, 1)) }));
        private static WorkPolicyState Policy(WorldState w, int pawn = 1) => w.WorkPolicies.Single(p => p.PawnId == pawn);
        private static void Only(WorldState w, WorkCategory category, int pawn = 1)
        {
            foreach (WorkCategory c in Enum.GetValues(typeof(WorkCategory))) Assert.That(w.SetWorkPriority(pawn, c, c == category ? 1 : 0), Is.True);
        }
        private static void Until(WorldState w, Func<bool> condition, int limit = 600)
        {
            for (var i = 0; i < limit && !condition(); i++) w.Step(1);
            Assert.That(condition(), Is.True, "Expected work-policy transition missing at Tick " + w.Tick);
        }
        private static WorkReadContext Context(WorldState w, WorkPolicyState policy)
        {
            var context = new WorkReadContext(new object(), w.Tick, 0, 0, w.Colonists[0]);
            context.SetPolicy(policy.CreateSnapshot()); return context;
        }
        private static WorkCandidate Candidate(WorkReadContext context, WorkCategory kind, WorkStep step, int id, int distance, long order = 0)
            => new WorkCandidate(context, kind, step, id, null, P(2, 1), order, Enumerable.Repeat(P(2, 1), distance).ToArray());

        [Test]
        public void DefaultsAndSnapshotsAreIndependentAndInvalidCommandsAreAtomic()
        {
            var w = World(two: true); var first = Policy(w); var second = Policy(w, 2); var snapshot = first.CreateSnapshot();
            foreach (WorkCategory kind in Enum.GetValues(typeof(WorkCategory))) Assert.That(first.GetPriority(kind), Is.EqualTo(kind == WorkCategory.Haul ? 1 : 2));
            Assert.That(w.SetWorkPriority(1, WorkCategory.Farm, 0), Is.True);
            Assert.That(first.GetPriority(WorkCategory.Farm), Is.Zero); Assert.That(second.GetPriority(WorkCategory.Farm), Is.EqualTo(2));
            Assert.That(snapshot.GetPriority(WorkCategory.Farm), Is.EqualTo(2));
            Assert.That(w.SetWorkPriority(1, WorkCategory.Farm, -1), Is.False);
            Assert.That(w.SetWorkPriority(1, WorkCategory.Farm, 4), Is.False);
            Assert.That(w.SetWorkPriority(1, (WorkCategory)99, 1), Is.False);
            Assert.That(w.SetWorkPriority(99, WorkCategory.Farm, 1), Is.False);
            Assert.That(first.GetPriority(WorkCategory.Farm), Is.Zero); Assert.That(w.Tick, Is.Zero);
        }

        [Test]
        public void SelectionUsesPriorityDistanceOrderTargetAndStepWithoutMutation()
        {
            var w = World(); var policy = new WorkPolicyState(1);
            foreach (WorkCategory category in Enum.GetValues(typeof(WorkCategory))) policy.SetPriority(category, 2);
            policy.SetPriority(WorkCategory.Cook, 1);
            var context = Context(w, policy);
            var other = Candidate(context, WorkCategory.Farm, WorkStep.Sow, 1, 0, 0);
            var far = Candidate(context, WorkCategory.Cook, WorkStep.Cook, 90, 4, 0);
            var late = Candidate(context, WorkCategory.Cook, WorkStep.Cook, 80, 2, 3);
            var highId = Candidate(context, WorkCategory.Cook, WorkStep.Cook, 70, 2, 1);
            var laterStep = Candidate(context, WorkCategory.Cook, WorkStep.Cook, 60, 2, 1);
            var expected = Candidate(context, WorkCategory.Cook, WorkStep.DeliverCook, 60, 2, 1);
            var candidates = new List<WorkCandidate> { other, far, late, highId, laterStep, expected };
            var action = w.Colonists[0].ActionId; var searches = w.PathSearchCount;
            Assert.That(WorkPolicy.Select(context, 1, candidates), Is.SameAs(expected));
            candidates.Reverse(); Assert.That(WorkPolicy.Select(context, 1, candidates), Is.SameAs(expected));
            Assert.That(w.Colonists[0].ActionId, Is.EqualTo(action)); Assert.That(w.PathSearchCount, Is.EqualTo(searches)); Assert.That(w.Tick, Is.Zero);
        }

        [Test]
        public void DisabledAndImpossibleCandidatesNeverWinForAnyCategory()
        {
            var w = World();
            foreach (WorkCategory category in Enum.GetValues(typeof(WorkCategory)))
            {
                var policy = new WorkPolicyState(1); policy.SetPriority(category, 0); var context = Context(w, policy);
                var off = Candidate(context, category, WorkStep.Gather, 10, 0);
                Assert.That(WorkPolicy.Select(context, 1, new[] { off }), Is.Null, category.ToString());
            }
            var enabledContext = Context(w, new WorkPolicyState(1));
            var blocked = Candidate(enabledContext, WorkCategory.Farm, WorkStep.Sow, 11, 0);
            enabledContext.SetFeatureInputs(new List<FeatureWorkOption> { new FeatureWorkOption(blocked, WorkReasonCode.Unreachable) });
            Assert.That(WorkPolicy.Select(enabledContext, 1, new[] { blocked }), Is.Null);
            Assert.That(WorkPolicy.Select(enabledContext, 2, new[] { blocked }), Is.Null);
        }

        [Test]
        public void HigherGatherPriorityOverridesExistingHaulFirstBehavior()
        {
            var w = World(); w.QueueDesignateGather(201, true); w.QueueCreateStorage(P(6, 1), P(6, 1));
            w.SetWorkPriority(1, WorkCategory.Gather, 1); w.SetWorkPriority(1, WorkCategory.Haul, 3); w.Step(1);
            Assert.That(w.Gatherables[0].OwnerId, Is.EqualTo(1)); Assert.That(w.Colonists[0].WorkTargetId, Is.EqualTo(201));
            Assert.That(w.Resources.All(r => !r.OwnerId.HasValue), Is.True);
        }

        [Test]
        public void ChangingEnabledPriorityKeepsCurrentActionButDisablingCancelsItImmediately()
        {
            var w = World(); w.QueueDesignateGather(201, true); w.Step(8);
            var pawn = w.Colonists[0]; var action = pawn.ActionId; var target = pawn.WorkTargetId; var progress = w.Gatherables[0].Progress; var tick = w.Tick;
            Assert.That(w.SetWorkPriority(1, WorkCategory.Gather, 3), Is.True);
            Assert.That(pawn.ActionId, Is.EqualTo(action)); Assert.That(pawn.WorkTargetId, Is.EqualTo(target));
            Assert.That(w.Gatherables[0].OwnerId, Is.EqualTo(1));
            Assert.That(w.SetWorkPriority(1, WorkCategory.Gather, 0), Is.True);
            Assert.That(pawn.IsWorking, Is.False); Assert.That(w.Gatherables[0].OwnerId, Is.Null);
            Assert.That(w.Gatherables[0].Progress, Is.EqualTo(progress)); Assert.That(w.Tick, Is.EqualTo(tick));
            w.Step(50); Assert.That(w.Gatherables[0].Progress, Is.EqualTo(progress));
            w.SetWorkPriority(1, WorkCategory.Gather, 1); Until(w, () => w.Gatherables[0].Depleted);
            Assert.That(w.ProducedAmount(ResourceKind.Wood), Is.EqualTo(10));
        }

        [Test]
        public void DisablingHaulDropsCargoOnceWithoutChangingTickOrTotal()
        {
            var w = World(); Only(w, WorkCategory.Haul); w.QueueCreateStorage(P(6, 1), P(6, 1));
            Until(w, () => w.Colonists[0].CargoAmount > 0); var tick = w.Tick;
            var total = w.ResourceTotal(ResourceKind.Wood, false) + w.ResourceTotal(ResourceKind.Wood, true) + w.CargoTotal(ResourceKind.Wood);
            w.SetWorkPriority(1, WorkCategory.Haul, 0); w.SetWorkPriority(1, WorkCategory.Haul, 0);
            Assert.That(w.Colonists[0].CargoAmount, Is.Zero); Assert.That(w.Colonists[0].IsWorking, Is.False);
            Assert.That(w.StorageZones.SelectMany(z => z.Slots).All(s => !s.OwnerId.HasValue), Is.True);
            Assert.That(w.ResourceTotal(ResourceKind.Wood, false) + w.ResourceTotal(ResourceKind.Wood, true) + w.CargoTotal(ResourceKind.Wood), Is.EqualTo(total));
            Assert.That(w.Tick, Is.EqualTo(tick));
        }

        [Test]
        public void DisablingBuildKeepsDeliveredMaterialsAndProgressForAnotherAction()
        {
            var w = World(); Only(w, WorkCategory.Build); w.PlaceBlueprint(BuildingKind.Wall, P(4, 1));
            var site = w.Blueprints.Single(); Until(w, () => site.Progress > 0);
            var progress = site.Progress; var materials = site.DeliveredWood; var tick = w.Tick;
            w.SetWorkPriority(1, WorkCategory.Build, 0);
            Assert.That(site.OwnerId, Is.Null); Assert.That(site.Progress, Is.EqualTo(progress)); Assert.That(site.DeliveredWood, Is.EqualTo(materials));
            Assert.That(w.Tick, Is.EqualTo(tick)); w.Step(100); Assert.That(site.Progress, Is.EqualTo(progress));
            w.SetWorkPriority(1, WorkCategory.Build, 1); Until(w, () => !site.Exists);
            Assert.That(w.Buildings.Single().Kind, Is.EqualTo(BuildingKind.Wall)); Assert.That(w.ConstructionConsumedWood, Is.EqualTo(2));
        }

        [Test]
        public void FarmDisabledForFirstPawnAllowsSecondToContinueSameProgress()
        {
            var w = World(two: true); Only(w, WorkCategory.Farm, 1); Only(w, WorkCategory.Farm, 2);
            w.CreateGrowingZone(P(2, 2), P(2, 2)); var cell = w.Crops.Single(); Until(w, () => cell.SowProgress > 0);
            var progress = cell.SowProgress; Assert.That(cell.OwnerId, Is.EqualTo(1));
            w.SetWorkPriority(1, WorkCategory.Farm, 0); Assert.That(cell.OwnerId, Is.Null); Assert.That(cell.SowProgress, Is.EqualTo(progress));
            Until(w, () => cell.OwnerId == 2); Until(w, () => cell.Stage == CropStage.Growing);
            Assert.That(Policy(w, 1).GetPriority(WorkCategory.Farm), Is.Zero); Assert.That(Policy(w, 2).GetPriority(WorkCategory.Farm), Is.EqualTo(1));
        }

        [Test]
        public void AllWorkDisabledStillAllowsEatingAndDraftStillOverridesEnabledWork()
        {
            var w = World(needs: true, nutrition: 3010);
            foreach (WorkCategory category in Enum.GetValues(typeof(WorkCategory))) w.SetWorkPriority(1, category, 0);
            w.Step(1); Assert.That(w.Colonists[0].LifeActivity, Is.Not.EqualTo(LifeActivity.Idle));
            w.ToggleDraft(1); w.SetWorkPriority(1, WorkCategory.Gather, 1); w.QueueDesignateGather(201, true); w.Step(30);
            Assert.That(w.Colonists[0].IsDrafted, Is.True); Assert.That(w.Colonists[0].IsWorking, Is.False); Assert.That(w.Gatherables[0].OwnerId, Is.Null);
        }

        private static WorldState CookingWorld()
        {
            var w = WorldState.Create(new ScenarioDefinition(8, 8, Array.Empty<GridPosition>(),
                new[] { new ColonistSeed(1, P(1, 1)) }, new[] {
                    new ResourceSeed(101, ResourceKind.Wood, 20, P(1, 1)),
                    new ResourceSeed(102, ResourceKind.Ingredients, 4, P(1, 2)),
                    new ResourceSeed(103, ResourceKind.Meal, 10, P(1, 3)) }, needsEnabled: false));
            Only(w, WorkCategory.Build); Assert.That(w.PlaceBlueprint(BuildingKind.Stove, P(4, 4)), Is.True);
            Until(w, () => w.Buildings.Any(b => b.Exists && b.Kind == BuildingKind.Stove));
            var stove = w.Buildings.Single(b => b.Exists && b.Kind == BuildingKind.Stove);
            Only(w, WorkCategory.Cook); Assert.That(w.SetCookingOrder(stove.Id, true, 12), Is.True);
            return w;
        }

        [Test]
        public void DisablingCookPreservesRealStoveInputAndProgressUntilReenabled()
        {
            var w = CookingWorld(); var order = w.CookingOrders.Single(o => o.Exists);
            Until(w, () => order.Progress > 0); var progress = order.Progress; var tick = w.Tick;
            Assert.That(order.InputIngredients, Is.EqualTo(2)); Assert.That(order.OwnerId, Is.EqualTo(1));
            w.SetWorkPriority(1, WorkCategory.Cook, 0);
            Assert.That(order.OwnerId, Is.Null); Assert.That(order.InputIngredients, Is.EqualTo(2));
            Assert.That(order.Progress, Is.EqualTo(progress)); Assert.That(w.Colonists[0].IsWorking, Is.False);
            Assert.That(w.Tick, Is.EqualTo(tick)); w.Step(100);
            Assert.That(order.InputIngredients, Is.EqualTo(2)); Assert.That(order.Progress, Is.EqualTo(progress));
            w.SetWorkPriority(1, WorkCategory.Cook, 1);
            Until(w, () => w.ResourceTotal(ResourceKind.Meal, false) + w.ResourceTotal(ResourceKind.Meal, true) + w.CargoTotal(ResourceKind.Meal) == 12);
            Assert.That(order.InputIngredients, Is.Zero);
            Assert.That(w.ResourceTotal(ResourceKind.Ingredients, false) + w.ResourceTotal(ResourceKind.Ingredients, true) + w.CargoTotal(ResourceKind.Ingredients), Is.Zero);
        }

        [Test]
        public void DisablingCookDuringDeliveryDropsIngredientsOnceAndKeepsTotal()
        {
            var w = CookingWorld(); var order = w.CookingOrders.Single(o => o.Exists);
            Until(w, () => w.Colonists[0].CargoKind == ResourceKind.Ingredients && w.Colonists[0].CargoAmount > 0);
            var tick = w.Tick; var before = w.ResourceTotal(ResourceKind.Ingredients, false) +
                w.ResourceTotal(ResourceKind.Ingredients, true) + w.CargoTotal(ResourceKind.Ingredients) + order.InputIngredients;
            Assert.That(before, Is.EqualTo(4)); w.SetWorkPriority(1, WorkCategory.Cook, 0); w.SetWorkPriority(1, WorkCategory.Cook, 0);
            Assert.That(w.Colonists[0].CargoAmount, Is.Zero); Assert.That(order.OwnerId, Is.Null);
            Assert.That(w.ResourceTotal(ResourceKind.Ingredients, false) + w.ResourceTotal(ResourceKind.Ingredients, true) +
                w.CargoTotal(ResourceKind.Ingredients) + order.InputIngredients, Is.EqualTo(before));
            Assert.That(w.Tick, Is.EqualTo(tick)); Assert.That(order.Progress, Is.Zero);
        }

        [Test]
        public void ConcreteDiagnosticWinsOverNoDesignationRegardlessOfOrder()
        {
            var policies = new[] { new WorkPolicyState(1), new WorkPolicyState(2) };
            var forward = new[] { WorkReasonCode.NoDesignation, WorkReasonCode.MissingMaterials };
            var reverse = new[] { WorkReasonCode.MissingMaterials, WorkReasonCode.NoDesignation };
            Assert.That(WorkDiagnostics.ForCategory(WorkCategory.Cook, policies, forward), Is.EqualTo(WorkReasonCode.MissingMaterials));
            Assert.That(WorkDiagnostics.ForCategory(WorkCategory.Cook, policies, reverse), Is.EqualTo(WorkReasonCode.MissingMaterials));
            Assert.That(WorkDiagnostics.ForCategory(WorkCategory.Cook, policies, new[] { WorkReasonCode.NoDesignation }), Is.EqualTo(WorkReasonCode.NoDesignation));
            Assert.That(WorkDiagnostics.ForCategory(WorkCategory.Cook, policies, new[] { WorkReasonCode.NoDesignation, WorkReasonCode.None }), Is.EqualTo(WorkReasonCode.None));
            Assert.That(WorkDiagnostics.ForCategory(WorkCategory.Cook, policies, new[] { WorkReasonCode.Disabled }), Is.EqualTo(WorkReasonCode.Disabled));
            Assert.That(WorkDiagnostics.ForCategory(WorkCategory.Cook, policies, new[] { WorkReasonCode.NoDesignation, WorkReasonCode.Disabled }), Is.EqualTo(WorkReasonCode.Disabled));
            Assert.That(WorkDiagnostics.ForCategory(WorkCategory.Cook, policies, new[] { WorkReasonCode.Disabled, WorkReasonCode.NoDesignation }), Is.EqualTo(WorkReasonCode.Disabled));
            Assert.That(WorkDiagnostics.ForCategory(WorkCategory.Cook, policies, new[] { WorkReasonCode.Disabled, WorkReasonCode.MissingMaterials }), Is.EqualTo(WorkReasonCode.MissingMaterials));
            Assert.That(WorkDiagnostics.ForCategory(WorkCategory.Cook, policies, new[] { WorkReasonCode.Disabled, WorkReasonCode.None }), Is.EqualTo(WorkReasonCode.None));
        }

        [Test]
        public void RealStoveDiagnosticChangesFromNoWorkerToMissingMaterialsWithoutTick()
        {
            var w = WorldState.Create(new ScenarioDefinition(8, 8, Array.Empty<GridPosition>(),
                new[] { new ColonistSeed(1, P(1, 1)) },
                new[] { new ResourceSeed(101, ResourceKind.Wood, 20, P(1, 1)) }, needsEnabled: false));
            Only(w, WorkCategory.Build); w.PlaceBlueprint(BuildingKind.Stove, P(4, 4));
            Until(w, () => w.Buildings.Any(b => b.Exists && b.Kind == BuildingKind.Stove));
            var stove = w.Buildings.Single(b => b.Exists && b.Kind == BuildingKind.Stove);
            w.SetCookingOrder(stove.Id, true, 12); var tick = w.Tick;
            Assert.That(w.GetWorkDiagnostic(WorkCategory.Cook, stove.Id), Is.EqualTo(WorkReasonCode.NoWorker));
            Assert.That(w.SetWorkPriority(1, WorkCategory.Cook, 1), Is.True);
            Assert.That(w.GetWorkDiagnostic(WorkCategory.Cook, stove.Id), Is.EqualTo(WorkReasonCode.MissingMaterials));
            Assert.That(w.GetWorkDiagnostic(WorkCategory.Cook, stove.Id), Is.EqualTo(WorkReasonCode.MissingMaterials));
            Assert.That(w.Tick, Is.EqualTo(tick)); Assert.That(w.Colonists[0].IsWorking, Is.False);
        }

        [Test]
        public void NoWorkerDiagnosticIsDistinctFromMissingMaterials()
        {
            var a = new WorkPolicyState(1); var b = new WorkPolicyState(2);
            a.SetPriority(WorkCategory.Cook, 0); b.SetPriority(WorkCategory.Cook, 0);
            var policies = new[] { a, b }; var reasons = new[] { WorkReasonCode.MissingMaterials };
            Assert.That(WorkDiagnostics.ForCategory(WorkCategory.Cook, policies, reasons), Is.EqualTo(WorkReasonCode.NoWorker));
            b.SetPriority(WorkCategory.Cook, 1);
            Assert.That(WorkDiagnostics.ForCategory(WorkCategory.Cook, policies, reasons), Is.EqualTo(WorkReasonCode.MissingMaterials));
            Assert.That(WorkDiagnostics.GetReasonText(WorkReasonCode.NoWorker), Is.EqualTo("담당자 없음"));
            Assert.That(WorkDiagnostics.GetReasonText(WorkReasonCode.MissingMaterials), Is.EqualTo("재료 부족"));
        }
    }
}
