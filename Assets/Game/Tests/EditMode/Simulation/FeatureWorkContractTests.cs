using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RimworldTweak.Simulation;

namespace RimworldTweak.Tests.EditMode
{
    public sealed class FeatureWorkContractTests
    {
        private static GridPosition P(int x, int y) => new GridPosition(x, y);
        private static WorldState World(bool haul = false) {
            var w = WorldState.Create(new ScenarioDefinition(8, 8, Array.Empty<GridPosition>(),
                new[] { new ColonistSeed(1, P(1, 1)), new ColonistSeed(2, P(1, 1)) },
                haul ? new[] { new ResourceSeed(201, ResourceKind.Wood, 12, P(2, 1)) } : Array.Empty<ResourceSeed>(),
                null, false, new[] { new GatherSeed(101, GatherKind.Tree, P(3, 1)) }));
            w.QueueDesignateGather(101, true);
            if (haul) w.QueueCreateStorage(P(6, 1), P(6, 1));
            return w;
        }
        [Test]
        public void ContextReadingIsPureAndSnapshotRemainsImmutable()
        {
            var w = World(); var pawn = w.Colonists[0]; var action = pawn.ActionId;
            var context = w.PrepareWorkContext(1); var searches = w.PathSearchCount;
            Assert.That(context.Candidates.Single().TargetId, Is.EqualTo(101));
            Assert.That(context.Candidates.Single().PathLength, Is.EqualTo(2));
            Assert.Throws<NotSupportedException>(() => ((IList<WorkCandidate>)context.Candidates).Clear());
            Assert.That(w.PathSearchCount, Is.EqualTo(searches));
            Assert.That(w.Tick, Is.Zero); Assert.That(pawn.ActionId, Is.EqualTo(action));
            Assert.That(w.Gatherables[0].OwnerId, Is.Null); Assert.That(w.Gatherables[0].Progress, Is.Zero);
            w.QueueDesignateGather(101, false);
            Assert.That(context.Candidates.Single().TargetId, Is.EqualTo(101));
            Assert.That(w.TryStartFeatureWork(1, context.Candidates[0]), Is.False);
            Assert.That(pawn.ActionId, Is.EqualTo(action));
        }
        [Test]
        public void CompetingCandidatesHaveOneOwnerAndLoserHasNoSideEffects()
        {
            var w = World(); var first = w.PrepareWorkContext(1); var second = w.PrepareWorkContext(2);
            var action = w.Colonists[1].ActionId;
            Assert.That(w.TryStartFeatureWork(1, first.Candidates[0]), Is.True);
            Assert.That(w.TryStartFeatureWork(2, second.Candidates[0]), Is.False);
            Assert.That(w.Gatherables[0].OwnerId, Is.EqualTo(1));
            Assert.That(w.Colonists[1].ActionId, Is.EqualTo(action));
            Assert.That(w.Colonists[1].IsWorking, Is.False);
        }
        [Test]
        public void WrongWorldPawnAndAdvancedTickRejectCandidates()
        {
            var w = World(); var c = w.PrepareWorkContext(1).Candidates[0];
            Assert.That(World().TryStartFeatureWork(1, c), Is.False);
            Assert.That(w.TryStartFeatureWork(2, c), Is.False);
            w.Step(1);
            Assert.That(w.TryStartFeatureWork(1, c), Is.False);
        }
        [Test]
        public void DraftAndStaleCancellationDoNotStartOrCancelNewAction()
        {
            var w = World(); var candidate = w.PrepareWorkContext(1).Candidates[0];
            w.ToggleDraft(1);
            Assert.That(w.TryStartFeatureWork(1, candidate), Is.False);
            Assert.That(w.PrepareWorkContext(1).Reason, Is.EqualTo(WorkReasonCode.Drafted));
            w.ToggleDraft(1);
            var fresh = w.PrepareWorkContext(1).Candidates[0];
            Assert.That(w.TryStartFeatureWork(1, fresh), Is.True);
            var pawn = w.Colonists[0]; var action = pawn.ActionId;
            w.CancelFeatureWork(1, action - 1, WorkCancelReason.Explicit);
            Assert.That(w.Gatherables[0].OwnerId, Is.EqualTo(1));
            w.QueueCancelLife(1, action);
            Assert.That(w.Gatherables[0].OwnerId, Is.Null);
            Assert.That(pawn.IsWorking, Is.False); Assert.That(w.Tick, Is.Zero);
        }
        [Test]
        public void LegacyHaulWinsAndDestinationIsClaimedOnlyAtPickup()
        {
            var w = World(true); var c = w.PrepareWorkContext(1).Candidates[0];
            Assert.That(c.Category, Is.EqualTo(WorkCategory.Haul)); Assert.That(c.SourceId, Is.EqualTo(201));
            Assert.That(w.TryStartFeatureWork(1, c), Is.True);
            Assert.That(w.Resources.Single().OwnerId, Is.EqualTo(1));
            Assert.That(w.StorageZones[0].Slots[0].OwnerId, Is.Null);
            Assert.That(w.Resources.Single().Amount, Is.EqualTo(12));
            for (var i = 0; i < 20 && w.Colonists[0].CargoAmount == 0; i++) w.Step(1);
            Assert.That(w.Colonists[0].CargoAmount, Is.EqualTo(10));
            w.QueueCancelLife(1); w.QueueCancelLife(1);
            Assert.That(w.ResourceTotal(ResourceKind.Wood, false) + w.ResourceTotal(ResourceKind.Wood, true) + w.CargoTotal(ResourceKind.Wood), Is.EqualTo(12));
            Assert.That(w.StorageZones[0].Slots[0].OwnerId, Is.Null);
        }
        [Test]
        public void FilterChangeInvalidatesPreparedHaulWithoutTakingResources()
        {
            var w = World(true); var c = w.PrepareWorkContext(1).Candidates[0];
            w.QueueStorageFilter(w.StorageZones[0].Id, ResourceKind.Wood, false);
            Assert.That(w.TryStartFeatureWork(1, c), Is.False);
            Assert.That(w.Resources.Single().OwnerId, Is.Null);
            Assert.That(w.Resources.Single().Amount, Is.EqualTo(12));
            Assert.That(w.PrepareWorkContext(1).Candidates.Single().Category, Is.EqualTo(WorkCategory.Gather));
        }
    }
}
