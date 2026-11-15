using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RimworldTweak.Simulation;

namespace RimworldTweak.Tests.EditMode
{
    public sealed class InitialWorldTests
    {
        private static ScenarioDefinition Scenario(int width = 8, int height = 8,
            GridPosition[] blocked = null, ColonistSeed[] colonists = null, ResourceSeed[] resources = null)
        {
            return new ScenarioDefinition(width, height, blocked ?? Array.Empty<GridPosition>(),
                colonists ?? Array.Empty<ColonistSeed>(), resources ?? Array.Empty<ResourceSeed>());
        }

        private static void Reject(ScenarioDefinition scenario, string code)
        {
            var error = Assert.Throws<ScenarioValidationException>(() => WorldState.Create(scenario));
            Assert.That(error.Code, Is.EqualTo(code));
        }

        [Test]
        public void SandboxHasSpecifiedInitialState()
        {
            var world = WorldState.Create(SandboxScenario.Create());
            Assert.That(world.Width, Is.EqualTo(8));
            Assert.That(world.Height, Is.EqualTo(8));
            Assert.That(world.BlockedTiles, Is.EqualTo(new[] { new GridPosition(3, 3) }));
            Assert.That(world.Colonists.Select(c => c.Id), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(world.Colonists.Select(c => c.Position), Is.EqualTo(new[] {
                new GridPosition(1, 1), new GridPosition(2, 1), new GridPosition(1, 2) }));
            Assert.That(world.Resources.Select(x => x.Id), Is.EqualTo(new[] { 101, 102 }));
            Assert.That(world.Resources.Select(x => x.Kind), Is.EqualTo(new[] { ResourceKind.Wood, ResourceKind.Meal }));
            Assert.That(world.Resources.Select(x => x.Amount), Is.EqualTo(new[] { 40, 18 }));
            Assert.That(world.Resources.Select(x => x.Position), Is.EqualTo(new[] { new GridPosition(4, 1), new GridPosition(4, 2) }));
        }

        [Test]
        public void RepeatedCreationProducesEquivalentIndependentSnapshots()
        {
            var scenario = SandboxScenario.Create();
            var a = WorldState.Create(scenario);
            var b = WorldState.Create(scenario);
            Assert.That(a, Is.Not.SameAs(b));
            Assert.That(a.BlockedTiles, Is.Not.SameAs(b.BlockedTiles));
            Assert.That(a.Colonists, Is.Not.SameAs(b.Colonists));
            Assert.That(a.Resources, Is.Not.SameAs(b.Resources));
            Assert.That(a.Colonists, Is.Not.SameAs(scenario.Colonists));
            Assert.That(a.Resources, Is.Not.SameAs(scenario.Resources));
            Assert.That(a.BlockedTiles, Is.EqualTo(b.BlockedTiles));
            Assert.That(a.Colonists.Select(c => (c.Id, c.Position)), Is.EqualTo(b.Colonists.Select(c => (c.Id, c.Position))));
            Assert.That(a.Resources.Select(x => (x.Id, x.Kind, x.Amount, x.Position)),
                Is.EqualTo(b.Resources.Select(x => (x.Id, x.Kind, x.Amount, x.Position))));
        }

        [Test]
        public void ChangingInputCollectionsCannotChangeScenarioOrWorlds()
        {
            var blocked = new List<GridPosition> { new GridPosition(3, 3) };
            var colonists = new List<ColonistSeed> { new ColonistSeed(1, new GridPosition(1, 1)) };
            var resources = new List<ResourceSeed> { new ResourceSeed(101, ResourceKind.Meal, 18, new GridPosition(4, 2)) };
            var scenario = new ScenarioDefinition(8, 8, blocked, colonists, resources);
            var a = WorldState.Create(scenario);
            blocked[0] = new GridPosition(-1, -1);
            colonists[0] = new ColonistSeed(-1, new GridPosition(0, 0));
            resources.Clear();
            var b = WorldState.Create(scenario);
            foreach (var world in new[] { a, b })
            {
                Assert.That(world.BlockedTiles[0], Is.EqualTo(new GridPosition(3, 3)));
                Assert.That(world.Colonists[0].Id, Is.EqualTo(1));
                Assert.That(world.Resources[0].Amount, Is.EqualTo(18));
            }
            Assert.That(scenario.BlockedTiles[0], Is.EqualTo(new GridPosition(3, 3)));
            Assert.That(scenario.Colonists[0].Id, Is.EqualTo(1));
            Assert.That(scenario.Resources[0].Amount, Is.EqualTo(18));
        }

        private static void AssertCannotWrite<T>(IReadOnlyList<T> values, T replacement)
        {
            Assert.That(values, Is.Not.InstanceOf<T[]>());
            if (values is IList<T> list)
            {
                Assert.That(list.IsReadOnly, Is.True);
                Assert.Throws<NotSupportedException>(() => list[0] = replacement);
                Assert.Throws<NotSupportedException>(() => list.Clear());
            }
        }

        [Test]
        public void ExposedCollectionsRejectMutation()
        {
            var scenario = SandboxScenario.Create();
            var world = WorldState.Create(scenario);
            AssertCannotWrite(scenario.BlockedTiles, new GridPosition(0, 0));
            AssertCannotWrite(scenario.Colonists, new ColonistSeed(9, new GridPosition(0, 0)));
            AssertCannotWrite(scenario.Resources, new ResourceSeed(9, ResourceKind.Wood, 1, new GridPosition(0, 0)));
            AssertCannotWrite(world.BlockedTiles, new GridPosition(0, 0));
            AssertCannotWrite(world.Colonists, new ColonistSeed(9, new GridPosition(0, 0)));
            AssertCannotWrite(world.Resources, new ResourceSeed(9, ResourceKind.Wood, 1, new GridPosition(0, 0)));
            Assert.That(world.Resources[0].Amount, Is.EqualTo(40));
        }

        [Test]
        public void InputOrderDoesNotChangeCanonicalWorldOrder()
        {
            var world = WorldState.Create(Scenario(blocked: new[] { new GridPosition(5, 2), new GridPosition(2, 1), new GridPosition(1, 1) },
                colonists: new[] { new ColonistSeed(2, new GridPosition(0, 0)), new ColonistSeed(1, new GridPosition(0, 1)) },
                resources: new[] { new ResourceSeed(102, ResourceKind.Meal, 2, new GridPosition(0, 2)), new ResourceSeed(101, ResourceKind.Wood, 4, new GridPosition(0, 3)) }));
            Assert.That(world.BlockedTiles, Is.EqualTo(new[] { new GridPosition(1, 1), new GridPosition(2, 1), new GridPosition(5, 2) }));
            Assert.That(world.Colonists.Select(c => c.Id), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(world.Resources.Select(x => x.Id), Is.EqualTo(new[] { 101, 102 }));
        }

        [Test]
        public void BoundaryPositionsAndSharedUnblockedTileAreAccepted()
        {
            var world = WorldState.Create(Scenario(colonists: new[] { new ColonistSeed(1, new GridPosition(0, 0)), new ColonistSeed(2, new GridPosition(7, 7)) },
                resources: new[] { new ResourceSeed(101, ResourceKind.Ingredients, 1, new GridPosition(7, 7)) }));
            Assert.That(world.Colonists.Count, Is.EqualTo(2));
            Assert.That(world.Resources[0].Position, Is.EqualTo(new GridPosition(7, 7)));
        }

        [Test]
        public void InvalidDimensionsAreRejected()
        {
            Reject(Scenario(width: 0), "InvalidDimensions");
            Reject(Scenario(height: -1), "InvalidDimensions");
        }

        [Test]
        public void InvalidEntityIdsAreRejected()
        {
            Reject(Scenario(colonists: new[] { new ColonistSeed(0, new GridPosition(0, 0)) }), "InvalidId");
            Reject(Scenario(resources: new[] { new ResourceSeed(-1, ResourceKind.Wood, 1, new GridPosition(0, 0)) }), "InvalidId");
        }

        [Test]
        public void DuplicateIdsAcrossAndWithinEntityTypesAreRejected()
        {
            Reject(Scenario(colonists: new[] { new ColonistSeed(1, new GridPosition(0, 0)), new ColonistSeed(1, new GridPosition(0, 1)) }), "DuplicateId");
            Reject(Scenario(colonists: new[] { new ColonistSeed(1, new GridPosition(0, 0)) }, resources: new[] { new ResourceSeed(1, ResourceKind.Wood, 1, new GridPosition(0, 1)) }), "DuplicateId");
            Reject(Scenario(resources: new[] { new ResourceSeed(1, ResourceKind.Wood, 1, new GridPosition(0, 0)), new ResourceSeed(1, ResourceKind.Meal, 2, new GridPosition(0, 1)) }), "DuplicateId");
        }

        [Test]
        public void OutOfBoundsPositionsAreRejectedForEveryEntityKind()
        {
            Reject(Scenario(blocked: new[] { new GridPosition(-1, 0) }), "OutOfBounds");
            Reject(Scenario(colonists: new[] { new ColonistSeed(1, new GridPosition(8, 0)) }), "OutOfBounds");
            Reject(Scenario(resources: new[] { new ResourceSeed(1, ResourceKind.Wood, 1, new GridPosition(0, 8)) }), "OutOfBounds");
        }

        [Test]
        public void InvalidResourceAmountOrKindIsRejected()
        {
            Reject(Scenario(resources: new[] { new ResourceSeed(1, ResourceKind.Wood, 0, new GridPosition(0, 0)) }), "InvalidAmount");
            Reject(Scenario(resources: new[] { new ResourceSeed(1, ResourceKind.Wood, -1, new GridPosition(0, 0)) }), "InvalidAmount");
            Reject(Scenario(resources: new[] { new ResourceSeed(1, (ResourceKind)999, 1, new GridPosition(0, 0)) }), "InvalidResourceKind");
        }

        [Test]
        public void NullEntitiesAreRejected()
        {
            Reject(Scenario(colonists: new ColonistSeed[] { null }), "NullEntity");
            Reject(Scenario(resources: new ResourceSeed[] { null }), "NullEntity");
        }

        [Test]
        public void DuplicateBlockedTilesAreRejected()
        {
            Reject(Scenario(blocked: new[] { new GridPosition(1, 1), new GridPosition(1, 1) }), "DuplicateBlockedTile");
        }

        [Test]
        public void EntitiesOnBlockedTilesAreRejected()
        {
            var blocked = new[] { new GridPosition(1, 1) };
            Reject(Scenario(blocked: blocked, colonists: new[] { new ColonistSeed(1, blocked[0]) }), "BlockedPosition");
            Reject(Scenario(blocked: blocked, resources: new[] { new ResourceSeed(1, ResourceKind.Wood, 1, blocked[0]) }), "BlockedPosition");
        }

        [Test]
        public void NullScenarioAndCollectionsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => WorldState.Create(null));
            Assert.Throws<ArgumentNullException>(() => new ScenarioDefinition(8, 8, null, Array.Empty<ColonistSeed>(), Array.Empty<ResourceSeed>()));
            Assert.Throws<ArgumentNullException>(() => new ScenarioDefinition(8, 8, Array.Empty<GridPosition>(), null, Array.Empty<ResourceSeed>()));
            Assert.Throws<ArgumentNullException>(() => new ScenarioDefinition(8, 8, Array.Empty<GridPosition>(), Array.Empty<ColonistSeed>(), null));
        }
    }
}
