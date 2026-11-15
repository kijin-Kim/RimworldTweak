using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RimworldTweak.Simulation;

namespace RimworldTweak.Tests.EditMode
{
    public sealed class FarmingTests
    {
        private static GridPosition P(int x, int y) => new GridPosition(x, y);
        private static WorldState World(ColonistSeed[] pawns = null, GridPosition[] blocked = null, ResourceSeed[] resources = null, bool needs = false)
            => WorldState.Create(new ScenarioDefinition(8, 8, blocked ?? Array.Empty<GridPosition>(),
                pawns ?? new[] { new ColonistSeed(1, P(1, 1)) }, resources ?? Array.Empty<ResourceSeed>(), needsEnabled: needs));
        private static void Until(WorldState world, Func<bool> condition, int limit)
        {
            for (var i = 0; i < limit && !condition(); i++) world.Step(1);
            Assert.That(condition(), Is.True, "Expected farming transition missing at Tick " + world.Tick);
        }
        private static CropState Cell(WorldState w) => w.Crops.Single(c => c.Exists);
        private static int Ingredients(WorldState w) => w.ResourceTotal(ResourceKind.Ingredients, false) +
            w.ResourceTotal(ResourceKind.Ingredients, true) + w.CargoTotal(ResourceKind.Ingredients);

        [Test]
        public void RectangleCreationIsAtomicAndConflictsWithExistingZones()
        {
            var w = World(blocked: new[] { P(4, 4) });
            Assert.That(w.CreateGrowingZone(P(3, 3), P(4, 4)), Is.False);
            Assert.That(w.GrowingZones.Count, Is.Zero); Assert.That(w.Crops.Count, Is.Zero);
            Assert.That(w.CreateGrowingZone(P(-1, 0), P(1, 1)), Is.False);
            Assert.That(w.CreateGrowingZone(P(2, 2), P(1, 1)), Is.True);
            Assert.That(w.GrowingZones.Single().Cells.Count, Is.EqualTo(4));
            Assert.That(w.Crops.Select(c => c.Position).Distinct().Count(), Is.EqualTo(4));
            Assert.That(w.CreateGrowingZone(P(2, 2), P(3, 3)), Is.False);
            Assert.That(w.Crops.Count, Is.EqualTo(4));
            Assert.That(w.QueueCreateStorage(P(1, 1), P(1, 1)), Is.False);
            Assert.That(w.QueueCreateStorage(P(6, 6), P(6, 6)), Is.True);
            Assert.That(w.CreateGrowingZone(P(6, 6), P(6, 6)), Is.False);
            Assert.That(w.Tick, Is.Zero);
        }

        [Test]
        public void SowGrowHarvestRespectExactBoundariesAndProduceSixOnce()
        {
            var w = World(); Assert.That(w.CreateGrowingZone(P(1, 1), P(1, 1)), Is.True);
            var cell = Cell(w); w.Step(1);
            Assert.That(cell.SowProgress, Is.Zero); Assert.That(cell.OwnerId, Is.EqualTo(1));
            w.Step(19); Assert.That(cell.Stage, Is.EqualTo(CropStage.Empty)); Assert.That(cell.SowProgress, Is.EqualTo(19));
            w.Step(1); Assert.That(cell.Stage, Is.EqualTo(CropStage.Growing)); Assert.That(cell.GrowthTicks, Is.Zero);
            var planted = cell.PlantedTick; Assert.That(planted, Is.EqualTo(w.Tick - 1));
            Assert.That(cell.OwnerId, Is.Null);
            w.SetSowingEnabled(w.GrowingZones[0].Id, false);
            w.Step(2399); Assert.That(cell.GrowthTicks, Is.EqualTo(2399)); Assert.That(cell.Stage, Is.EqualTo(CropStage.Growing));
            w.Step(1); Assert.That(cell.Stage, Is.EqualTo(CropStage.Mature)); Assert.That(cell.HarvestProgress, Is.Zero);
            w.Step(29); Assert.That(Ingredients(w), Is.Zero); Assert.That(cell.HarvestProgress, Is.EqualTo(29));
            w.Step(1); Assert.That(Ingredients(w), Is.EqualTo(6)); Assert.That(w.ProducedAmount(ResourceKind.Ingredients), Is.EqualTo(6));
            Assert.That(cell.Stage, Is.EqualTo(CropStage.Empty)); Assert.That(cell.OwnerId, Is.Null);
            w.Step(2500); Assert.That(Ingredients(w), Is.EqualTo(6)); Assert.That(cell.SowProgress, Is.Zero);
        }

        [Test]
        public void DraftReleasesSowingClaimAndSecondPawnContinuesProgress()
        {
            var w = World(new[] { new ColonistSeed(1, P(1, 1)), new ColonistSeed(2, P(1, 1)) });
            w.CreateGrowingZone(P(1, 1), P(1, 1)); w.Step(11); var cell = Cell(w);
            Assert.That(cell.SowProgress, Is.EqualTo(10)); Assert.That(cell.OwnerId, Is.EqualTo(1));
            var tick = w.Tick; var action = w.Colonists[0].ActionId;
            w.ToggleDraft(1); Assert.That(w.Tick, Is.EqualTo(tick)); Assert.That(cell.OwnerId, Is.Null);
            Assert.That(cell.SowProgress, Is.EqualTo(10)); Assert.That(w.Colonists[0].IsWorking, Is.False);
            w.Step(1); Assert.That(cell.OwnerId, Is.EqualTo(2)); Assert.That(cell.SowProgress, Is.EqualTo(10));
            w.QueueCancelLife(1, action); Assert.That(cell.OwnerId, Is.EqualTo(2));
            w.Step(10); Assert.That(cell.Stage, Is.EqualTo(CropStage.Growing));
        }

        [Test]
        public void SowingSwitchCancelsImmediatelyAndKeepsCropGrowthIndependentOfPawn()
        {
            var w = World(); w.CreateGrowingZone(P(1, 1), P(1, 1)); w.Step(9);
            var cell = Cell(w); var progress = cell.SowProgress; var tick = w.Tick; var zone = w.GrowingZones[0];
            Assert.That(w.SetSowingEnabled(zone.Id, false), Is.True);
            Assert.That(w.Tick, Is.EqualTo(tick)); Assert.That(cell.OwnerId, Is.Null);
            Assert.That(w.Colonists[0].IsWorking, Is.False); w.Step(30); Assert.That(cell.SowProgress, Is.EqualTo(progress));
            w.SetSowingEnabled(zone.Id, true); Until(w, () => cell.Stage == CropStage.Growing, 40);
            w.ToggleDraft(1); w.SetSowingEnabled(zone.Id, false); w.Step(2400);
            Assert.That(cell.Stage, Is.EqualTo(CropStage.Mature)); Assert.That(cell.OwnerId, Is.Null);
            Assert.That(Ingredients(w), Is.Zero);
        }

        [Test]
        public void DeletingGrowingZoneCancelsWorkButNeverDeletesLooseResources()
        {
            var w = World(); w.CreateGrowingZone(P(1, 1), P(1, 1)); w.Step(21);
            var zone = w.GrowingZones[0]; var cell = Cell(w); w.Step(2400);
            Until(w, () => cell.HarvestProgress > 0, 20); var tick = w.Tick;
            Assert.That(w.DeleteGrowingZone(zone.Id), Is.True);
            Assert.That(w.Tick, Is.EqualTo(tick)); Assert.That(zone.Exists, Is.False); Assert.That(cell.Exists, Is.False);
            Assert.That(cell.OwnerId, Is.Null); Assert.That(w.Colonists[0].IsWorking, Is.False);
            Assert.That(Ingredients(w), Is.Zero); Assert.That(w.DeleteGrowingZone(zone.Id), Is.False);
            Assert.That(w.CreateGrowingZone(P(1, 1), P(1, 1)), Is.True);
            var next = Cell(w); Until(w, () => Ingredients(w) == 6, 2500);
            w.SetSowingEnabled(next.ZoneId, false); Assert.That(w.DeleteGrowingZone(next.ZoneId), Is.True);
            Assert.That(Ingredients(w), Is.EqualTo(6)); w.Step(100); Assert.That(Ingredients(w), Is.EqualTo(6));
        }

        [Test]
        public void HarvestFeedsExistingHaulingAndCellCanBeSownAgain()
        {
            var w = World(); w.CreateGrowingZone(P(1, 1), P(1, 1)); w.QueueCreateStorage(P(4, 1), P(4, 1));
            var cell = Cell(w); var firstOrder = cell.RequestOrder;
            Until(w, () => w.ResourceTotal(ResourceKind.Ingredients, true) == 6, 2600);
            Assert.That(w.ProducedAmount(ResourceKind.Ingredients), Is.EqualTo(6)); Assert.That(Ingredients(w), Is.EqualTo(6));
            Until(w, () => cell.Stage == CropStage.Growing, 100);
            Assert.That(cell.RequestOrder, Is.GreaterThan(firstOrder)); Assert.That(cell.GrowthTicks, Is.LessThan(2400));
        }

        [Test]
        public void UnreachableGrowingCellRecoversAfterPathIsOpened()
        {
            var blocked = Enumerable.Range(0, 8).Select(y => P(3, y)).ToArray();
            var w = World(blocked: blocked); w.CreateGrowingZone(P(5, 1), P(5, 1)); w.Step(30); var cell = Cell(w);
            Assert.That(cell.OwnerId, Is.Null); Assert.That(cell.SowProgress, Is.Zero);
            Assert.That(w.QueueSetBlocked(P(3, 1), false), Is.True);
            Until(w, () => cell.Stage == CropStage.Growing, 100);
            Assert.That(cell.OwnerId, Is.Null);
        }

        [Test]
        public void HungerInterruptsFarmingAndReleasesItsClaimForLifeAction()
        {
            var w = World(new[] { new ColonistSeed(1, P(1, 1), 3100, 9000) },
                resources: new[] { new ResourceSeed(101, ResourceKind.Meal, 10, P(1, 1)) }, needs: true);
            w.CreateGrowingZone(P(2, 1), P(2, 1)); w.Step(1); var cell = Cell(w);
            Assert.That(cell.OwnerId, Is.EqualTo(1));
            Until(w, () => w.Colonists[0].LifeActivity != LifeActivity.Idle, 100);
            Assert.That(cell.OwnerId, Is.Null); Assert.That(w.Colonists[0].IsWorking, Is.False);
            Assert.That(w.Colonists[0].LifeTargetId, Is.EqualTo(101));
        }

        [Test]
        public void CandidateRulesFilterCategoryAndFailureWithoutMutatingOptions()
        {
            var w = World(); var pawn = w.Colonists[0];
            var context = new WorkReadContext(new object(), 0, 0, 0, pawn);
            var route = new[] { P(2, 1) };
            var sow = new WorkCandidate(context, WorkCategory.Farm, WorkStep.Sow, 101, null, P(2, 1), 1, route);
            var blocked = new WorkCandidate(context, WorkCategory.Farm, WorkStep.Harvest, 102, null, P(2, 1), 2, route);
            var build = new WorkCandidate(context, WorkCategory.Build, WorkStep.Construct, 103, null, P(2, 1), 3, route);
            context.SetFeatureInputs(new List<FeatureWorkOption> {
                new FeatureWorkOption(sow, WorkReasonCode.None),
                new FeatureWorkOption(blocked, WorkReasonCode.TargetReserved),
                new FeatureWorkOption(build, WorkReasonCode.None) });
            var beforeAction = pawn.ActionId; var beforeSearches = w.PathSearchCount;
            var result = FarmingRules.GetCandidates(context, pawn.Id);
            Assert.That(result.Count, Is.EqualTo(1)); Assert.That(result[0], Is.SameAs(sow));
            Assert.That(FarmingRules.GetCandidates(context, pawn.Id + 1).Count, Is.Zero);
            Assert.That(FarmingRules.GetCandidates(context, pawn.Id)[0], Is.SameAs(sow));
            Assert.That(context.Options.Count, Is.EqualTo(3));
            Assert.That(context.Options[1].Reason, Is.EqualTo(WorkReasonCode.TargetReserved));
            Assert.That(w.PathSearchCount, Is.EqualTo(beforeSearches)); Assert.That(pawn.ActionId, Is.EqualTo(beforeAction));
            Assert.That(w.Tick, Is.Zero); Assert.That(pawn.IsWorking, Is.False);
        }

        [Test]
        public void RulesDoNotMutateSnapshotAndRejectRepeatedCompletedStep()
        {
            var w = World(); var pawn = w.Colonists[0];
            var context = new WorkReadContext(new object(), 100, 0, 0, pawn);
            var snapshot = new FeatureWorkSnapshot(1, pawn.ActionId, 99, WorkStep.Harvest, 29, 30, 0, WorkReasonCode.None);
            context.SetFeatureInputs(new List<FeatureWorkOption>(), snapshot);
            var result = FarmingRules.EvaluateStep(context, 1); var again = FarmingRules.EvaluateStep(context, 1);
            Assert.That(snapshot.Progress, Is.EqualTo(29)); Assert.That(w.Tick, Is.Zero);
            Assert.That(result.Progress, Is.EqualTo(30)); Assert.That(result.Completed, Is.True);
            Assert.That(result.ProduceAmount, Is.EqualTo(6)); Assert.That(again.ProduceAmount, Is.EqualTo(6));
            var complete = new WorkReadContext(new object(), 101, 0, 0, pawn);
            complete.SetFeatureInputs(new List<FeatureWorkOption>(), new FeatureWorkSnapshot(1, pawn.ActionId, 99, WorkStep.Harvest, 30, 30, 0, WorkReasonCode.None));
            Assert.That(FarmingRules.EvaluateStep(complete, 1).ProduceAmount, Is.Zero);
            Assert.That(FarmingRules.NextGrowthTicks(0, 7, 7), Is.Zero);
            Assert.That(FarmingRules.NextGrowthTicks(2399, 7, 2407), Is.EqualTo(2400));
        }
    }
}
