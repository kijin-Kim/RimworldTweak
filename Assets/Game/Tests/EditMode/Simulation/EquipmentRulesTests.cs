using System;
using NUnit.Framework;
using RimworldTweak.Simulation;

namespace RimworldTweak.Tests.EditMode
{
    public sealed class EquipmentRulesTests
    {
        [Test]
        public void PrototypeWeaponsProvideDistinctRangedRolesAndMelee()
        {
            var rifle = PrototypeEquipment.Rifle;
            var carbine = PrototypeEquipment.Carbine;
            var knife = PrototypeEquipment.Knife;
            Assert.That(rifle.Kind, Is.EqualTo(WeaponKind.Ranged));
            Assert.That(carbine.Kind, Is.EqualTo(WeaponKind.Ranged));
            Assert.That(knife.Kind, Is.EqualTo(WeaponKind.Melee));
            Assert.That(rifle.Id, Is.Not.EqualTo(carbine.Id));
            Assert.That(knife.Id, Is.Not.EqualTo(rifle.Id).And.Not.EqualTo(carbine.Id));
            Assert.That(rifle.Range, Is.GreaterThan(carbine.Range));
            Assert.That(rifle.AimTicks, Is.GreaterThan(carbine.AimTicks));
            Assert.That(rifle.RecoveryTicks, Is.GreaterThan(carbine.RecoveryTicks));
            Assert.That(knife.Range, Is.EqualTo(1));
            Assert.That(knife.AimTicks, Is.Zero);
            Assert.That(PrototypeEquipment.LightArmor.Reduction, Is.EqualTo(5));
        }

        [Test]
        public void WeaponRejectsInvalidIdentityKindRangeDamageAndTiming()
        {
            foreach (var id in new[] { null, "", " \t" })
                Assert.Throws<ArgumentException>(() => new WeaponDefinition(id, WeaponKind.Ranged, 1, 1, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponDefinition("w", (WeaponKind)2, 1, 1, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponDefinition("w", (WeaponKind)(-1), 1, 1, 0, 1));
            foreach (var range in new[] { 0, -1, int.MinValue })
                Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponDefinition("w", WeaponKind.Ranged, range, 1, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponDefinition("w", WeaponKind.Ranged, 1, -1, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponDefinition("w", WeaponKind.Ranged, 1, 1, -1, 1));
            foreach (var recovery in new[] { 0, -1 })
                Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponDefinition("w", WeaponKind.Ranged, 1, 1, 0, recovery));
        }

        [Test]
        public void ArmorAndDamageRejectInvalidInputs()
        {
            foreach (var id in new[] { null, "", " \t" })
                Assert.Throws<ArgumentException>(() => new ArmorDefinition(id, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ArmorDefinition("a", -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DamageInput(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DamageInput(int.MinValue));
        }

        [Test]
        public void ZeroDamageAndZeroProtectionAreValid()
        {
            var weapon = new WeaponDefinition("zero", WeaponKind.Ranged, 1, 0, 0, 1);
            var result = ArmorRules.Resolve(new DamageInput(weapon.Damage), new ArmorDefinition("none", 0));
            Assert.That(result.OriginalDamage, Is.Zero);
            Assert.That(result.AbsorbedDamage, Is.Zero);
            Assert.That(result.RemainingDamage, Is.Zero);
            Assert.That(ArmorRules.Resolve(default, PrototypeEquipment.LightArmor).RemainingDamage, Is.Zero);
        }

        [Test]
        public void NoArmorPreservesAllDamage()
        {
            var result = ArmorRules.Resolve(new DamageInput(20), null);
            Assert.That(result.OriginalDamage, Is.EqualTo(20));
            Assert.That(result.AbsorbedDamage, Is.Zero);
            Assert.That(result.RemainingDamage, Is.EqualTo(20));
        }

        [Test]
        public void PartialArmorAbsorbsExactlyOnceWithoutChangingInput()
        {
            var input = new DamageInput(20);
            var armor = new ArmorDefinition("armor", 5);
            var result = ArmorRules.Resolve(input, armor);
            Assert.That(result.OriginalDamage, Is.EqualTo(20));
            Assert.That(result.AbsorbedDamage, Is.EqualTo(5));
            Assert.That(result.RemainingDamage, Is.EqualTo(15));
            Assert.That(input.Amount, Is.EqualTo(20));
            Assert.That(armor.Reduction, Is.EqualTo(5));
            Assert.That(typeof(DamageInput), Is.Not.EqualTo(typeof(DamageResult)));
            Assert.That(typeof(ArmorRules).GetMethod("Resolve").GetParameters()[0].ParameterType, Is.EqualTo(typeof(DamageInput)));
        }

        [Test]
        public void EqualOrExcessArmorCannotCreateNegativeDamage()
        {
            foreach (var reduction in new[] { 20, 21, int.MaxValue })
            {
                var result = ArmorRules.Resolve(new DamageInput(20), new ArmorDefinition("armor", reduction));
                Assert.That(result.OriginalDamage, Is.EqualTo(20));
                Assert.That(result.AbsorbedDamage, Is.EqualTo(20));
                Assert.That(result.RemainingDamage, Is.Zero);
            }
        }

        [Test]
        public void MaximumDamagePreservesAccountingWithoutOverflow()
        {
            foreach (var reduction in new[] { 0, 1, int.MaxValue })
            {
                var result = ArmorRules.Resolve(new DamageInput(int.MaxValue), new ArmorDefinition("armor", reduction));
                Assert.That(result.RemainingDamage, Is.EqualTo(int.MaxValue - reduction));
                Assert.That((long)result.AbsorbedDamage + result.RemainingDamage, Is.EqualTo((long)int.MaxValue));
            }
            Assert.That(ArmorRules.Resolve(new DamageInput(int.MaxValue), null).RemainingDamage, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void RepeatedResolutionIsDeterministicAndDoesNotAccumulateProtection()
        {
            var input = new DamageInput(PrototypeEquipment.Rifle.Damage);
            for (var i = 0; i < 100; i++)
            {
                var result = ArmorRules.Resolve(input, PrototypeEquipment.LightArmor);
                Assert.That(result.OriginalDamage, Is.EqualTo(20));
                Assert.That(result.AbsorbedDamage, Is.EqualTo(5));
                Assert.That(result.RemainingDamage, Is.EqualTo(15));
            }
        }

        [Test]
        public void PublicDataPropertiesCannotBeReassigned()
        {
            foreach (var type in new[] { typeof(WeaponDefinition), typeof(ArmorDefinition), typeof(DamageInput), typeof(DamageResult) })
                foreach (var property in type.GetProperties())
                    Assert.That(property.SetMethod, Is.Null, type.Name + "." + property.Name);
        }
    }
}
