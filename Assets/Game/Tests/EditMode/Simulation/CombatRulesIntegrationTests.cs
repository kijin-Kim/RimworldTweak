using System;
using NUnit.Framework;
using RimworldTweak.Simulation;
using UnityEngine;

namespace RimworldTweak.Tests.EditMode
{
    public sealed class CombatRulesIntegrationTests
    {
        private static GridPosition P(int x, int y) => new GridPosition(x, y);

        // Models the caller's gate, not actual firing or a random hit roll.
        private static DamageResult? ResolveAssumedHit(WeaponDefinition weapon,
            GridPosition source, GridPosition target, Func<GridPosition, SightCell> map,
            ArmorDefinition armor, out SightResult sight)
        {
            sight = SightRules.Evaluate(source, target, weapon.Range, map);
            return sight.CanAttack ? ArmorRules.Resolve(new DamageInput(weapon.Damage), armor) : (DamageResult?)null;
        }

        [Test]
        public void BlockedAndOutOfRangeAttacksNeverProduceDamage()
        {
            var weapon = new WeaponDefinition("integration", WeaponKind.Ranged, 5, 20, 1, 1);
            var blocked = ResolveAssumedHit(weapon, P(0, 0), P(4, 0),
                p => new SightCell(p == P(2, 0), 0), null, out var sight);
            Assert.That(blocked.HasValue, Is.False);
            Assert.That(sight.Reason, Is.EqualTo(SightFailure.Blocked));
            var distant = ResolveAssumedHit(weapon, P(0, 0), P(6, 0),
                p => new SightCell(false, 0), null, out sight);
            Assert.That(distant.HasValue, Is.False);
            Assert.That(sight.Reason, Is.EqualTo(SightFailure.OutOfRange));
        }

        [Test]
        public void FrontCoverChangesSightInformationButDoesNotReduceDamageTwice()
        {
            var weapon = new WeaponDefinition("integration", WeaponKind.Ranged, 8, 20, 1, 1);
            var armor = new ArmorDefinition("integration-armor", 6);
            Func<GridPosition, SightCell> map = p => new SightCell(false, p == P(3, 0) ? 50 : 0);
            var front = ResolveAssumedHit(weapon, P(0, 0), P(4, 0), map, armor, out var frontSight);
            var side = ResolveAssumedHit(weapon, P(4, 4), P(4, 0), map, armor, out var sideSight);
            Assert.That(frontSight.CoverPercent, Is.EqualTo(50));
            Assert.That(sideSight.CoverPercent, Is.Zero);
            Assert.That(front.Value.OriginalDamage, Is.EqualTo(20));
            Assert.That(front.Value.AbsorbedDamage, Is.EqualTo(6));
            Assert.That(front.Value.RemainingDamage, Is.EqualTo(14));
            Assert.That(side.Value.RemainingDamage, Is.EqualTo(14));
        }

        [Test]
        public void RepeatedRuleQueriesPreserveLiveWorldAndSerializedSave()
        {
            var session = new SimulationSession(new ScenarioDefinition(8, 8, new[] { P(3, 2) },
                new[] { new ColonistSeed(1, P(1, 1)) },
                new[] { new ResourceSeed(101, ResourceKind.Wood, 20, P(2, 1)) }, null, false));
            session.AdvanceTime(0.037m);
            var before = session.CaptureSaveData(true, 3);
            Assert.That(before.Success, Is.True);
            var json = JsonUtility.ToJson(before.Data);
            var weapon = PrototypeEquipment.Rifle;
            for (var i = 0; i < 100; i++)
            {
                var damage = ResolveAssumedHit(weapon, P(1, 1), P(2, 1),
                    p => new SightCell(false, 0), PrototypeEquipment.LightArmor, out var sight);
                Assert.That(sight.CanAttack, Is.True);
                Assert.That(damage.Value.RemainingDamage,
                    Is.EqualTo(Math.Max(0, weapon.Damage - PrototypeEquipment.LightArmor.Reduction)));
            }
            Assert.That(JsonUtility.ToJson(session.CaptureSaveData(true, 3).Data), Is.EqualTo(json));
            Assert.That(SimulationSession.TryRestoreSave(before.Data, out var restored, out var error), Is.True, error);
            session.World.Step(5);
            restored.World.Step(5);
            Assert.That(JsonUtility.ToJson(session.CaptureSaveData(true, 3).Data),
                Is.EqualTo(JsonUtility.ToJson(restored.CaptureSaveData(true, 3).Data)));
        }
    }
}
