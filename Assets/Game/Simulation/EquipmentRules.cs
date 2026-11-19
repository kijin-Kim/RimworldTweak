using System;

namespace RimworldTweak.Simulation
{
    public enum WeaponKind { Ranged, Melee }

    /// <summary>Validated, immutable weapon data; it does not equip or mutate a pawn.</summary>
    public sealed class WeaponDefinition
    {
        public WeaponDefinition(string id, WeaponKind kind, int range, int damage, int aimTicks, int recoveryTicks)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Weapon ID is required.", nameof(id));
            if (kind != WeaponKind.Ranged && kind != WeaponKind.Melee) throw new ArgumentOutOfRangeException(nameof(kind));
            if (range <= 0) throw new ArgumentOutOfRangeException(nameof(range));
            if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage));
            if (aimTicks < 0) throw new ArgumentOutOfRangeException(nameof(aimTicks));
            if (recoveryTicks <= 0) throw new ArgumentOutOfRangeException(nameof(recoveryTicks));
            Id = id;
            Kind = kind;
            Range = range;
            Damage = damage;
            AimTicks = aimTicks;
            RecoveryTicks = recoveryTicks;
        }

        public string Id { get; }
        public WeaponKind Kind { get; }
        public int Range { get; }
        public int Damage { get; }
        public int AimTicks { get; }
        public int RecoveryTicks { get; }
    }

    public sealed class ArmorDefinition
    {
        public ArmorDefinition(string id, int reduction)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Armor ID is required.", nameof(id));
            if (reduction < 0) throw new ArgumentOutOfRangeException(nameof(reduction));
            Id = id;
            Reduction = reduction;
        }

        public string Id { get; }
        public int Reduction { get; }
    }

    /// <summary>Unmitigated damage. Default value is a valid zero-damage attack.</summary>
    public readonly struct DamageInput
    {
        public DamageInput(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Amount = amount;
        }

        public int Amount { get; }
    }

    /// <summary>Resolved damage cannot be passed directly back to ArmorRules.Resolve.</summary>
    public readonly struct DamageResult
    {
        internal DamageResult(int originalDamage, int absorbedDamage)
        {
            OriginalDamage = originalDamage;
            AbsorbedDamage = absorbedDamage;
            RemainingDamage = originalDamage - absorbedDamage;
        }

        public int OriginalDamage { get; }
        public int AbsorbedDamage { get; }
        public int RemainingDamage { get; }
    }

    public static class ArmorRules
    {
        /// <summary>Apply flat protection exactly once to this input; null means no armor.</summary>
        public static DamageResult Resolve(DamageInput input, ArmorDefinition armor)
        {
            var absorbed = armor == null ? 0 : Math.Min(input.Amount, armor.Reduction);
            return new DamageResult(input.Amount, absorbed);
        }
    }

    /// <summary>Small deterministic fixtures for prototyping, not final game balance.</summary>
    public static class PrototypeEquipment
    {
        public static WeaponDefinition Rifle { get; } = new WeaponDefinition("rifle", WeaponKind.Ranged, 12, 20, 20, 30);
        public static WeaponDefinition Carbine { get; } = new WeaponDefinition("carbine", WeaponKind.Ranged, 8, 12, 10, 15);
        public static WeaponDefinition Knife { get; } = new WeaponDefinition("knife", WeaponKind.Melee, 1, 8, 0, 10);
        public static ArmorDefinition LightArmor { get; } = new ArmorDefinition("light-armor", 5);
    }
}
