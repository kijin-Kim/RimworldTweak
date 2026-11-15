using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RimworldTweak.Simulation;

namespace RimworldTweak.Tests.EditMode
{
    public sealed class MovementTests
    {
        private static GridPosition P(int x, int y) => new GridPosition(x, y);
        private static ScenarioDefinition Scenario(IEnumerable<GridPosition> blocked = null)
        {
            return new ScenarioDefinition(8, 8, blocked ?? Array.Empty<GridPosition>(),
                new[] { new ColonistSeed(1, P(1, 1)), new ColonistSeed(2, P(1, 2)), new ColonistSeed(3, P(2, 2)) },
                new[] { new ResourceSeed(101, ResourceKind.Wood, 40, P(4, 1)) });
        }
        private static WorldState World(IEnumerable<GridPosition> blocked = null) => WorldState.Create(Scenario(blocked));
        private static void SameState(WorldState a, WorldState b)
        {
            Assert.That(a.Tick, Is.EqualTo(b.Tick));
            Assert.That(a.BlockedTiles, Is.EqualTo(b.BlockedTiles));
            Assert.That(a.PendingCommandCount, Is.EqualTo(b.PendingCommandCount));
            for (var i = 0; i < a.Colonists.Count; i++)
            {
                var x = a.Colonists[i]; var y = b.Colonists[i];
                Assert.That((x.Id, x.Position, x.Destination, x.MovementProgress, x.Status, x.HasPendingCommand, x.PendingDestination),
                    Is.EqualTo((y.Id, y.Position, y.Destination, y.MovementProgress, y.Status, y.HasPendingCommand, y.PendingDestination)));
                Assert.That(x.Path, Is.EqualTo(y.Path));
            }
            Assert.That(a.Resources.Select(r => (r.Id, r.Amount, r.Position)), Is.EqualTo(b.Resources.Select(r => (r.Id, r.Amount, r.Position))));
        }

        [Test]
        public void StraightMovementArrivesAtTwentyTicksAndLeavesResourcesUnchanged()
        {
            var w = World(); var c = w.Colonists[0];
            Assert.That(w.QueueMove(1, P(5, 1)), Is.True);
            Assert.That(c.HasPendingCommand, Is.True);
            Assert.That(c.PendingDestination, Is.EqualTo(P(5, 1)));
            w.Step(0);
            Assert.That(w.PendingCommandCount, Is.EqualTo(1));
            w.Step(4);
            Assert.That(c.Position, Is.EqualTo(P(1, 1))); Assert.That(c.MovementProgress, Is.EqualTo(4));
            w.Step(1);
            Assert.That(c.Position, Is.EqualTo(P(2, 1))); Assert.That(c.MovementProgress, Is.Zero);
            w.Step(14);
            Assert.That(c.Position, Is.EqualTo(P(4, 1))); Assert.That(c.Status, Is.EqualTo(MovementStatus.Moving));
            w.Step(1);
            Assert.That(c.Position, Is.EqualTo(P(5, 1))); Assert.That(c.Status, Is.EqualTo(MovementStatus.Arrived));
            Assert.That(c.Path, Is.Empty); Assert.That(c.MovementProgress, Is.Zero); Assert.That(c.HasPendingCommand, Is.False);
            w.Step(80);
            Assert.That(c.Position, Is.EqualTo(P(5, 1))); Assert.That(w.Resources[0].Amount, Is.EqualTo(40));
        }

        [Test]
        public void DetourIsShortestDeterministicAndNeverEntersBlockedTile()
        {
            var w = World(new[] { P(3, 1) });
            var expected = new[] { P(2, 1), P(2, 2), P(3, 2), P(4, 2), P(5, 2), P(5, 1) };
            Assert.That(w.FindPath(P(1, 1), P(5, 1)), Is.EqualTo(expected));
            Assert.That(w.FindPath(P(1, 1), P(5, 1)), Is.EqualTo(expected));
            w.QueueMove(1, P(5, 1));
            for (var i = 0; i < 29; i++) { w.Step(1); Assert.That(w.Colonists[0].Position, Is.Not.EqualTo(P(3, 1))); }
            Assert.That(w.Colonists[0].Position, Is.Not.EqualTo(P(5, 1)));
            w.Step(1); Assert.That(w.Colonists[0].Position, Is.EqualTo(P(5, 1)));
        }

        [Test]
        public void PathHandlesSameTileBoundsBlockedAndDisconnectedTargets()
        {
            var wall = Enumerable.Range(0, 8).Select(y => P(3, y)); var w = World(wall);
            Assert.That(w.FindPath(P(1, 1), P(1, 1)), Is.Empty);
            Assert.That(w.FindPath(P(1, 1), P(5, 1)), Is.Null);
            Assert.That(w.FindPath(P(1, 1), P(3, 1)), Is.Null);
            Assert.That(w.FindPath(P(-1, 1), P(2, 1)), Is.Null);
            Assert.That(w.FindPath(P(1, 1), P(8, 1)), Is.Null);
            w.QueueMove(1, P(1, 1)); w.Step(1);
            Assert.That(w.Colonists[0].Status, Is.EqualTo(MovementStatus.Arrived));
            Assert.That(w.Colonists[0].MovementProgress, Is.Zero);
        }

        [Test]
        public void InvalidCommandsDoNotOverwriteAValidPendingMove()
        {
            var w = World(new[] { P(3, 3) });
            Assert.That(w.QueueMove(1, P(5, 1)), Is.True);
            Assert.That(w.QueueMove(99, P(2, 1)), Is.False); Assert.That(w.LastCommandError, Is.EqualTo("InvalidColonist"));
            Assert.That(w.QueueMove(1, P(-1, 1)), Is.False); Assert.That(w.LastCommandError, Is.EqualTo("OutOfBounds"));
            Assert.That(w.QueueMove(1, P(3, 3)), Is.False); Assert.That(w.LastCommandError, Is.EqualTo("BlockedDestination"));
            Assert.That(w.QueueStop(99), Is.False);
            Assert.That(w.PendingCommandCount, Is.EqualTo(1));
            w.Step(20); Assert.That(w.Colonists[0].Position, Is.EqualTo(P(5, 1)));
        }

        [Test]
        public void LastReachableCommandWinsAndUnreachablePreservesPreviousMove()
        {
            var w = World(Enumerable.Range(0, 8).Select(y => P(3, y)));
            w.QueueMove(1, P(2, 1)); w.QueueMove(1, P(2, 3)); w.QueueMove(1, P(5, 1));
            w.Step(1);
            Assert.That(w.Colonists[0].Destination, Is.EqualTo(P(2, 3)));
            Assert.That(w.LastCommandError, Is.EqualTo("Unreachable"));
            Assert.That(w.Colonists[0].MovementProgress, Is.EqualTo(1));
            Assert.That(w.Colonists[0].Position, Is.EqualTo(P(1, 1)));
            w.QueueMove(1, P(5, 1)); w.Step(1);
            Assert.That(w.Colonists[0].Destination, Is.EqualTo(P(2, 3)));
            Assert.That(w.Colonists[0].MovementProgress, Is.EqualTo(2));
            w.Step(13); Assert.That(w.Colonists[0].Position, Is.EqualTo(P(2, 3)));
        }

        [Test]
        public void RepeatingTargetPreservesProgressRetargetRestartsAndStopConsumesNoMovement()
        {
            var w = World(); var c = w.Colonists[0];
            w.QueueMove(1, P(5, 1)); w.Step(2);
            var searches = w.PathSearchCount;
            w.QueueMove(1, P(5, 1)); w.Step(1);
            Assert.That(c.MovementProgress, Is.EqualTo(3)); Assert.That(w.PathSearchCount, Is.EqualTo(searches));
            w.QueueMove(1, P(1, 5)); w.Step(1);
            Assert.That(c.MovementProgress, Is.EqualTo(1)); Assert.That(c.Position, Is.EqualTo(P(1, 1)));
            w.Step(3); Assert.That(c.MovementProgress, Is.EqualTo(4));
            w.QueueStop(1); Assert.That(c.PendingDestination, Is.Null); Assert.That(c.HasPendingCommand, Is.False);
            Assert.That(c.IsDrafted, Is.True);
            w.Step(1);
            Assert.That(c.Position, Is.EqualTo(P(1, 1))); Assert.That(c.Status, Is.EqualTo(MovementStatus.Idle));
            Assert.That(c.Destination, Is.Null); Assert.That(c.Path, Is.Empty); Assert.That(c.MovementProgress, Is.Zero);
        }

        [Test]
        public void BatchAndPartitionedTicksProduceIdenticalFullMovementState()
        {
            var a = World(new[] { P(3, 1) }); var b = World(new[] { P(3, 1) });
            foreach (var w in new[] { a, b }) { w.QueueMove(1, P(5, 1)); w.QueueMove(2, P(5, 5)); }
            a.Step(13); for (var i = 0; i < 13; i++) b.Step(1); SameState(a, b);
            foreach (var w in new[] { a, b }) w.QueueMove(1, P(6, 6));
            a.Step(87); for (var i = 0; i < 87; i++) b.Step(1); SameState(a, b);
        }

        [Test]
        public void BlockingCorridorWaitsWithoutRepeatedSearchAndOpeningResumes()
        {
            var w = World(); var c = w.Colonists[0];
            w.QueueMove(1, P(5, 1)); w.Step(2);
            foreach (var y in Enumerable.Range(0, 8)) Assert.That(w.QueueSetBlocked(P(3, y), true), Is.True);
            Assert.That(w.BlockedTiles, Is.Empty);
            w.Step(1);
            Assert.That(c.Status, Is.EqualTo(MovementStatus.Blocked)); Assert.That(c.Destination, Is.EqualTo(P(5, 1)));
            Assert.That(c.Position, Is.EqualTo(P(1, 1))); Assert.That(c.MovementProgress, Is.Zero);
            var searches = w.PathSearchCount; var revision = w.MapRevision;
            w.Step(20); Assert.That(w.PathSearchCount, Is.EqualTo(searches)); Assert.That(w.MapRevision, Is.EqualTo(revision));
            Assert.That(w.QueueSetBlocked(P(3, 1), false), Is.True);
            w.Step(1); Assert.That(c.Status, Is.EqualTo(MovementStatus.Moving)); Assert.That(c.MovementProgress, Is.EqualTo(1));
            w.Step(19); Assert.That(c.Position, Is.EqualTo(P(5, 1)));
        }

        [Test]
        public void MapChangeDetoursBeforeEnteringNewObstacleAndNoOpDoesNotDelay()
        {
            var w = World(); var c = w.Colonists[0];
            w.QueueMove(1, P(5, 1)); w.Step(4);
            w.QueueSetBlocked(P(2, 1), true); w.Step(1);
            Assert.That(c.Position, Is.EqualTo(P(1, 1))); Assert.That(c.Status, Is.EqualTo(MovementStatus.Moving));
            Assert.That(c.Path, Has.No.Member(P(2, 1)));
            var revision = w.MapRevision; var searches = w.PathSearchCount;
            w.QueueSetBlocked(P(2, 1), true); w.Step(1);
            Assert.That(w.MapRevision, Is.EqualTo(revision)); Assert.That(w.PathSearchCount, Is.EqualTo(searches));
            Assert.That(c.MovementProgress, Is.EqualTo(2));
            w.Step(28); Assert.That(c.Position, Is.EqualTo(P(5, 1)));
        }

        [Test]
        public void OccupiedTileCannotBeBlockedAndMapChangesPrecedeMovementCommands()
        {
            var w = World();
            Assert.That(w.QueueSetBlocked(P(1, 1), true), Is.False); Assert.That(w.LastCommandError, Is.EqualTo("OccupiedTile"));
            Assert.That(w.QueueSetBlocked(P(8, 1), true), Is.False);
            w.QueueMove(1, P(5, 1)); w.QueueSetBlocked(P(5, 1), true); w.Step(1);
            Assert.That(w.Colonists[0].Position, Is.EqualTo(P(1, 1)));
            Assert.That(w.Colonists[0].Status, Is.Not.EqualTo(MovementStatus.Moving));
            Assert.That(w.PendingCommandCount, Is.Zero);
        }

        [Test]
        public void MovementStatePathsAndCommandsAreIndependentAndExternallyReadOnly()
        {
            var scenario = Scenario(); var a = WorldState.Create(scenario); var b = WorldState.Create(scenario);
            Assert.That(a.Colonists[0], Is.Not.SameAs(b.Colonists[0]));
            a.QueueMove(1, P(5, 1)); a.Step(5);
            Assert.That(a.Colonists[0].Position, Is.EqualTo(P(2, 1)));
            Assert.That(b.Colonists[0].Position, Is.EqualTo(P(1, 1))); Assert.That(b.PendingCommandCount, Is.Zero);
            Assert.That(scenario.Colonists[0].Position, Is.EqualTo(P(1, 1)));
            Assert.That(a.Colonists[1].Position, Is.EqualTo(scenario.Colonists[1].Position));
            Assert.That(a.Colonists[0].Path, Is.Not.SameAs(b.Colonists[0].Path));
            foreach (var property in typeof(ColonistState).GetProperties()) Assert.That(property.GetSetMethod(false), Is.Null, property.Name);
            var path = a.Colonists[0].Path;
            Assert.That(path, Is.Not.InstanceOf<GridPosition[]>());
            if (path is IList<GridPosition> list) Assert.Throws<NotSupportedException>(() => list[0] = P(7, 7));
            var returned = a.FindPath(P(1, 1), P(5, 1));
            if (returned is IList<GridPosition> returnedList) Assert.Throws<NotSupportedException>(() => returnedList.Clear());
        }

        [Test]
        public void ResetClearsMovingBlockedAndPendingStatesWithoutMutatingOldWorld()
        {
            var session = new SimulationSession(Scenario());
            for (var i = 0; i < 10; i++)
            {
                var old = session.World; old.QueueMove(1, P(5, 1));
                if (i % 3 != 0) { session.AdvanceTime(0.25m); if (i % 3 == 2) { foreach (var y in Enumerable.Range(0, 8)) old.QueueSetBlocked(P(3, y), true); old.Step(1); } }
                var oldTick = old.Tick; var oldPosition = old.Colonists[0].Position;
                session.Reset(); var fresh = session.World;
                Assert.That(fresh, Is.Not.SameAs(old)); Assert.That(fresh.Tick, Is.Zero); Assert.That(session.PendingSeconds, Is.Zero);
                Assert.That(fresh.PendingCommandCount, Is.Zero); Assert.That(fresh.BlockedTiles, Is.Empty); Assert.That(fresh.LastCommandError, Is.Empty);
                Assert.That(fresh.Colonists[0].Position, Is.EqualTo(P(1, 1))); Assert.That(fresh.Colonists[0].Path, Is.Empty);
                Assert.That(fresh.Colonists[0].Destination, Is.Null); Assert.That(fresh.Colonists[0].MovementProgress, Is.Zero);
                Assert.That(fresh.Colonists[0].HasPendingCommand, Is.False); Assert.That(fresh.Colonists[0].Status, Is.EqualTo(MovementStatus.Idle));
                fresh.Step(1); Assert.That(old.Tick, Is.EqualTo(oldTick)); Assert.That(old.Colonists[0].Position, Is.EqualTo(oldPosition));
            }
            Assert.That(session.ResetCount, Is.EqualTo(10));
        }
    }
}
