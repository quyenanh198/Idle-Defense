using System;

namespace IdleHeroDefense.Domain
{
    public enum TargetingRule { FirstEnemy, LowestHealthEnemy, AllEnemies }
    public enum StatusEffectType { None, Burn, Stun }

    public sealed class StatusEffectDefinition
    {
        public StatusEffectType Type { get; }
        public float Duration { get; }
        public int DamagePerTick { get; }
        public float TickInterval { get; }

        public StatusEffectDefinition(StatusEffectType type, float duration, int damagePerTick = 0, float tickInterval = 1f)
        {
            if (duration < 0) throw new ArgumentOutOfRangeException(nameof(duration));
            if (tickInterval <= 0) throw new ArgumentOutOfRangeException(nameof(tickInterval));
            Type = type;
            Duration = duration;
            DamagePerTick = Math.Max(0, damagePerTick);
            TickInterval = tickInterval;
        }
    }

    public sealed class AbilityDefinition
    {
        public string Id { get; }
        public int Damage { get; }
        public TargetingRule Targeting { get; }
        public StatusEffectDefinition StatusEffect { get; }

        public AbilityDefinition(string id, int damage, TargetingRule targeting = TargetingRule.FirstEnemy,
            StatusEffectDefinition statusEffect = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Ability id is required.", nameof(id));
            Id = id;
            Damage = Math.Max(0, damage);
            Targeting = targeting;
            StatusEffect = statusEffect;
        }
    }
}

