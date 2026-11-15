using System;
using System.Linq;
using NUnit.Framework;
using RimworldTweak.Simulation;

namespace RimworldTweak.Tests.EditMode
{
    public sealed class TickTests
    {
        [Test]
        public void ExplicitStepsReachExactlyOneHundredWithoutChangingEntities()
        {
            var world = WorldState.Create(SandboxScenario.Create());
            var positions = world.Colonists.Select(x => x.Position).ToArray();
            var amounts = world.Resources.Select(x => x.Amount).ToArray();
            Assert.That(world.Tick, Is.Zero);
            for (var i = 0; i < 100; i++) world.Step(1);
            world.Step(0);
            Assert.That(world.Tick, Is.EqualTo(100L));
            Assert.That(world.Colonists.Select(x => x.Position), Is.EqualTo(positions));
            Assert.That(world.Resources.Select(x => x.Amount), Is.EqualTo(amounts));
        }

        [Test]
        public void DecimalPartitionsProduceExactlyOneHundredTicksAndSameRemainder()
        {
            var whole = new SimulationSession(SandboxScenario.Create());
            var parts = new SimulationSession(SandboxScenario.Create());
            whole.AdvanceTime(10.037m);
            for (var i = 0; i < 1000; i++) parts.AdvanceTime(0.01m);
            parts.AdvanceTime(0.037m);
            Assert.That(whole.World.Tick, Is.EqualTo(100L));
            Assert.That(parts.World.Tick, Is.EqualTo(whole.World.Tick));
            Assert.That(whole.PendingSeconds, Is.EqualTo(0.037m));
            Assert.That(parts.PendingSeconds, Is.EqualTo(whole.PendingSeconds));
        }

        [Test]
        public void FractionalRemainderCrossesBoundaryWithoutLosingTime()
        {
            var session = new SimulationSession(SandboxScenario.Create());
            session.AdvanceTime(0.099m);
            Assert.That(session.World.Tick, Is.Zero);
            Assert.That(session.PendingSeconds, Is.EqualTo(0.099m));
            session.AdvanceTime(0m);
            session.AdvanceTime(0.002m);
            Assert.That(session.World.Tick, Is.EqualTo(1L));
            Assert.That(session.PendingSeconds, Is.EqualTo(0.001m));
            session.AdvanceTime(0.099m);
            Assert.That(session.World.Tick, Is.EqualTo(2L));
            Assert.That(session.PendingSeconds, Is.Zero);
        }

        [Test]
        public void NegativeStepAndTimeRejectWithoutMutation()
        {
            var session = new SimulationSession(SandboxScenario.Create());
            session.AdvanceTime(0.15m);
            Assert.Throws<ArgumentOutOfRangeException>(() => session.World.Step(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.Step(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.AdvanceTime(-0.01m));
            Assert.That(session.World.Tick, Is.EqualTo(1L));
            Assert.That(session.PendingSeconds, Is.EqualTo(0.05m));
        }

        [Test]
        public void TickOverflowFailsBeforeChangingState()
        {
            var session = new SimulationSession(SandboxScenario.Create());
            var tickField = typeof(WorldState).GetField("<Tick>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(tickField, Is.Not.Null);
            tickField.SetValue(session.World, long.MaxValue);
            session.World.SetDrafted(1,true);
            session.World.QueueMove(1, new GridPosition(5, 1));
            Assert.Throws<OverflowException>(() => session.Step(1));
            Assert.That(session.World.PendingCommandCount, Is.EqualTo(1));
            Assert.That(session.World.Colonists[0].MovementProgress, Is.Zero);
            Assert.That(session.World.Tick, Is.EqualTo(long.MaxValue));
            Assert.Throws<OverflowException>(() => session.AdvanceTime(0.1m));
            Assert.That(session.World.Tick, Is.EqualTo(long.MaxValue));
            Assert.That(session.PendingSeconds, Is.Zero);
            Assert.Throws<OverflowException>(() => session.AdvanceTime(decimal.MaxValue));
            Assert.That(session.World.Tick, Is.EqualTo(long.MaxValue));
            Assert.That(session.PendingSeconds, Is.Zero);
        }

        [Test]
        public void IndependentSessionsAndResetDoNotMutatePreviousWorld()
        {
            var scenario = SandboxScenario.Create();
            var first = new SimulationSession(scenario);
            var second = new SimulationSession(scenario);
            first.AdvanceTime(1.05m);
            var oldWorld = first.World;
            first.Reset();
            Assert.That(first.ResetCount, Is.EqualTo(1));
            Assert.That(first.World, Is.Not.SameAs(oldWorld));
            Assert.That(first.World.Tick, Is.Zero);
            Assert.That(first.PendingSeconds, Is.Zero);
            Assert.That(oldWorld.Tick, Is.EqualTo(10L));
            Assert.That(second.World.Tick, Is.Zero);
            first.Step(100);
            Assert.That(first.World.Tick, Is.EqualTo(100L));
            Assert.That(oldWorld.Tick, Is.EqualTo(10L));
            Assert.That(second.World.Tick, Is.Zero);
            first.Reset();
            Assert.That(first.ResetCount, Is.EqualTo(2));
            Assert.That(first.World.Resources.Select(x => x.Amount), Is.EqualTo(new[] { 40, 18 }));
        }
    }
}
