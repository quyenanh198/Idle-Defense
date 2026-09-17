using System;

namespace IdleHeroDefense.Domain
{
    public sealed class BattleUnit
    {
        public string Id { get; }
        public int MaxHealth { get; }
        public int Health { get; private set; }
        public int Attack { get; }
        public float AttackInterval { get; }
        public bool IsAlive => Health > 0;
        public float AttackCooldown { get; private set; }

        public BattleUnit(string id, int maxHealth, int attack, float attackInterval)
        {
            Id = id;
            MaxHealth = maxHealth;
            Health = maxHealth;
            Attack = attack;
            AttackInterval = attackInterval;
        }

        public bool TickAttack(float deltaTime)
        {
            if (!IsAlive) return false;
            AttackCooldown -= Math.Max(0, deltaTime);
            if (AttackCooldown > 0) return false;
            AttackCooldown += AttackInterval;
            return true;
        }

        public int TakeDamage(int amount)
        {
            var applied = Math.Min(Health, Math.Max(0, amount));
            Health -= applied;
            return applied;
        }

        public int Heal(int amount)
        {
            var applied = Math.Min(MaxHealth - Health, Math.Max(0, amount));
            Health += applied;
            return applied;
        }
    }
}

