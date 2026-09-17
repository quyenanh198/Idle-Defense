using System;

namespace IdleHeroDefense.Domain
{
    [Serializable]
    public sealed class EnemyDefinition
    {
        public string Id { get; }
        public int MaxHealth { get; }
        public int Attack { get; }
        public float AttackInterval { get; }
        public bool IsBoss { get; }

        public EnemyDefinition(string id, int maxHealth, int attack, float attackInterval, bool isBoss = false)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Enemy id is required.", nameof(id));
            if (maxHealth <= 0) throw new ArgumentOutOfRangeException(nameof(maxHealth));
            if (attackInterval <= 0) throw new ArgumentOutOfRangeException(nameof(attackInterval));
            Id = id;
            MaxHealth = maxHealth;
            Attack = Math.Max(0, attack);
            AttackInterval = attackInterval;
            IsBoss = isBoss;
        }
    }
}

