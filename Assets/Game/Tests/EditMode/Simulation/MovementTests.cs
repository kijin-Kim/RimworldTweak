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
        private static bool Move(WorldState world,int id,GridPosition target)
        {
            var pawn=world.Colonists.FirstOrDefault(p=>p.Id==id);
            if(pawn!=null&&!pawn.IsDrafted) world.SetDrafted(id,true);
            return world.QueueMove(id,target);
        }
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
        public void WeightedPathsMatchIndependentRelaxationAcrossBothMapSizes()
        {
            const int height = 8;
            var wall = Enumerable.Range(0,height).Where(y => y != 5).Select(y=>P(17,y)).ToArray();
            var blocked = new HashSet<GridPosition>(wall);
            const int width = 39;
            var costs = new int[width,height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++) costs[x,y] = 100000;
            costs[0,0] = 0;
            // Independent reference uses repeated full-grid relaxation, with no priority queue or parents.
            bool changed;
            do
            {
                changed = false;
                for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    int nx=x+dx, ny=y+dy;
                    if (nx<0 || nx>=width || ny<0 || ny>=height || blocked.Contains(P(nx,ny))) continue;
                    if (dx!=0 && dy!=0 && (blocked.Contains(P(nx,y)) || blocked.Contains(P(x,ny)))) continue;
                    int cost=costs[x,y]+(dx!=0 && dy!=0 ? 7 : 5);
                    if (cost >= costs[nx,ny]) continue;
                    costs[nx,ny]=cost;
                    changed=true;
                }
            } while(changed);
            WorldState CreateMap(int size) => WorldState.Create(new ScenarioDefinition(size,height,wall,
                new[]{new ColonistSeed(1,P(0,0))},Array.Empty<ResourceSeed>()));
            var dense=CreateMap(250); var sparse=CreateMap(251);
            var goals = new[]{P(36,7),P(12,2),P(15,4),P(23,0),P(34,1)};
            foreach (var goal in goals.Concat(goals.Reverse()))
            {
                var route=dense.FindPath(P(0,0),goal);
                Assert.That(sparse.FindPath(P(0,0),goal),Is.EqualTo(route));
                Assert.That(CreateMap(250).FindPath(P(0,0),goal),Is.EqualTo(route));
                var previous=P(0,0);
                var cost=0;
                foreach (var cell in route)
                {
                    int dx=Math.Abs(cell.X-previous.X), dy=Math.Abs(cell.Y-previous.Y);
                    Assert.That(Math.Max(dx,dy),Is.EqualTo(1));
                    Assert.That(blocked.Contains(cell),Is.False);
                    if (dx*dy!=0)
                    {
                        Assert.That(blocked.Contains(P(cell.X,previous.Y)),Is.False);
                        Assert.That(blocked.Contains(P(previous.X,cell.Y)),Is.False);
                    }
                    cost+=dx*dy==0?5:7; previous=cell;
                }
                Assert.That(previous,Is.EqualTo(goal));
                Assert.That(cost,Is.EqualTo(costs[goal.X,goal.Y]));
            }
        }

        [Test]
        public void DiagonalTimingCornerClearanceAndRestore()
        {
            foreach (bool starving in new[] { false, true })
            {
                var scenario = new ScenarioDefinition(5, 5, Array.Empty<GridPosition>(),
                    new[] { new ColonistSeed(1, P(0,0), starving ? 0 : 9000, 9000) },
                    Array.Empty<ResourceSeed>(), null, true);
                var session = new SimulationSession(scenario);
                var world = session.World;
                Assert.That(Move(world, 1, P(3,3)), Is.True);
                int elapsed = starving ? 9 : 4;
                session.Step(elapsed);
                Assert.That(world.Colonists[0].MovementTicksPerTile, Is.EqualTo(starving ? 14 : 7));
                var data = session.CaptureSaveData().Data;
                Assert.That(SimulationSession.TryRestoreSave(data, out var restored, out var error), Is.True, error);
                for (var tick = elapsed; tick < (starving ? 42 : 21); tick++)
                {
                    Assert.That(world.Colonists[0].Position, Is.Not.EqualTo(P(3,3)));
                    session.Step(1); restored.Step(1);
                    SameState(world, restored.World);
                }
                Assert.That(world.Colonists[0].Position, Is.EqualTo(P(3,3)));
                data.World.Colonists[0].MovementTicksPerTile = 5;
                Assert.That(SimulationSession.TryRestoreSave(data, out _, out _), Is.False);
            }
            var corner = WorldState.Create(new ScenarioDefinition(2,2,new[]{P(1,0)},
                new[]{new ColonistSeed(1,P(0,0))}, Array.Empty<ResourceSeed>()));
            Assert.That(corner.FindPath(P(0,0),P(1,1)), Is.EqualTo(new[]{P(0,1),P(1,1)}));
            corner.QueueSetBlocked(P(0,1),true); corner.Step(1);
            Assert.That(corner.FindPath(P(0,0),P(1,1)), Is.Null);
        }

        [Test]
        public void StraightMovementArrivesAtTwentyTicksAndLeavesResourcesUnchanged()
        {
            var w = World(); var c = w.Colonists[0];
            Assert.That(Move(w, 1, P(5, 1)), Is.True);
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
            var expected = new[] { P(2, 2), P(3, 2), P(4, 2), P(5, 1) };
            Assert.That(w.FindPath(P(1, 1), P(5, 1)), Is.EqualTo(expected));
            Assert.That(w.FindPath(P(1, 1), P(5, 1)), Is.EqualTo(expected));
            Move(w, 1, P(5, 1));
            for (var i = 0; i < 23; i++) { w.Step(1); Assert.That(w.Colonists[0].Position, Is.Not.EqualTo(P(3, 1))); }
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
            Move(w, 1, P(1, 1)); w.Step(1);
            Assert.That(w.Colonists[0].Status, Is.EqualTo(MovementStatus.Arrived));
            Assert.That(w.Colonists[0].MovementProgress, Is.Zero);
        }

        [Test]
        public void InvalidCommandsDoNotOverwriteAValidPendingMove()
        {
            var w = World(new[] { P(3, 3) });
            Assert.That(Move(w, 1, P(5, 1)), Is.True);
            Assert.That(Move(w, 99, P(2, 1)), Is.False); Assert.That(w.LastCommandError, Is.EqualTo("InvalidColonist"));
            Assert.That(Move(w, 1, P(-1, 1)), Is.False); Assert.That(w.LastCommandError, Is.EqualTo("OutOfBounds"));
            Assert.That(Move(w, 1, P(3, 3)), Is.False); Assert.That(w.LastCommandError, Is.EqualTo("BlockedDestination"));
            Assert.That(w.QueueStop(99), Is.False);
            Assert.That(w.PendingCommandCount, Is.EqualTo(1));
            w.Step(20); Assert.That(w.Colonists[0].Position, Is.EqualTo(P(5, 1)));
        }

        [Test]
        public void LastReachableCommandWinsAndUnreachablePreservesPreviousMove()
        {
            var w = World(Enumerable.Range(0, 8).Select(y => P(3, y)));
            Move(w, 1, P(2, 1)); Move(w, 1, P(2, 3)); Move(w, 1, P(5, 1));
            w.Step(1);
            Assert.That(w.Colonists[0].Destination, Is.EqualTo(P(2, 3)));
            Assert.That(w.LastCommandError, Is.EqualTo("Unreachable"));
            Assert.That(w.Colonists[0].MovementProgress, Is.EqualTo(1));
            Assert.That(w.Colonists[0].Position, Is.EqualTo(P(1, 1)));
            Move(w, 1, P(5, 1)); w.Step(1);
            Assert.That(w.Colonists[0].Destination, Is.EqualTo(P(2, 3)));
            Assert.That(w.Colonists[0].MovementProgress, Is.EqualTo(2));
            w.Step(13); Assert.That(w.Colonists[0].Position, Is.EqualTo(P(2, 3)));
        }

        [Test]
        public void RepeatingTargetPreservesProgressRetargetRestartsAndStopConsumesNoMovement()
        {
            var w = World(); var c = w.Colonists[0];
            Move(w, 1, P(5, 1)); w.Step(2);
            var searches = w.PathSearchCount;
            Move(w, 1, P(5, 1)); w.Step(1);
            Assert.That(c.MovementProgress, Is.EqualTo(3)); Assert.That(w.PathSearchCount, Is.EqualTo(searches));
            Move(w, 1, P(1, 5)); w.Step(1);
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
            foreach (var w in new[] { a, b }) { Move(w, 1, P(5, 1)); Move(w, 2, P(5, 5)); }
            a.Step(13); for (var i = 0; i < 13; i++) b.Step(1); SameState(a, b);
            foreach (var w in new[] { a, b }) Move(w, 1, P(6, 6));
            a.Step(87); for (var i = 0; i < 87; i++) b.Step(1); SameState(a, b);
        }

        [Test]
        public void BlockingCorridorWaitsWithoutRepeatedSearchAndOpeningResumes()
        {
            var w = World(); var c = w.Colonists[0];
            Move(w, 1, P(5, 1)); w.Step(2);
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
            Move(w, 1, P(5, 1)); w.Step(4);
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
            Move(w, 1, P(5, 1)); w.QueueSetBlocked(P(5, 1), true); w.Step(1);
            Assert.That(w.Colonists[0].Position, Is.EqualTo(P(1, 1)));
            Assert.That(w.Colonists[0].Status, Is.Not.EqualTo(MovementStatus.Moving));
            Assert.That(w.PendingCommandCount, Is.Zero);
        }

        [Test]
        public void MovementStatePathsAndCommandsAreIndependentAndExternallyReadOnly()
        {
            var scenario = Scenario(); var a = WorldState.Create(scenario); var b = WorldState.Create(scenario);
            Assert.That(a.Colonists[0], Is.Not.SameAs(b.Colonists[0]));
            Move(a, 1, P(5, 1)); a.Step(5);
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
                var old = session.World; Move(old, 1, P(5, 1));
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
