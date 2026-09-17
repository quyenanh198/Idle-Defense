using System;
using UnityEngine;

namespace IdleHeroDefense.Domain
{
    public enum HeroClass { Tank, Warrior, Ranger, Mage, Support }
    public enum Faction { Nature, Shadow, Light, Machine, Beast }

    [Serializable]
    public sealed class HeroDefinition
    {
        [SerializeField] private string id = "hero";
        [SerializeField] private string displayName = "Hero";
        [SerializeField] private HeroClass heroClass;
        [SerializeField] private Faction faction;
        [Min(1)] [SerializeField] private int maxHealth = 100;
        [Min(0)] [SerializeField] private int attack = 10;
        [Min(0.1f)] [SerializeField] private float attackInterval = 1f;
        [Min(0)] [SerializeField] private int ultimateDamage = 50;
        [Min(1)] [SerializeField] private int ultimateEnergy = 100;

        public string Id => id;
        public string DisplayName => displayName;
        public HeroClass Class => heroClass;
        public Faction Faction => faction;
        public int MaxHealth => maxHealth;
        public int Attack => attack;
        public float AttackInterval => attackInterval;
        public int UltimateDamage => ultimateDamage;
        public int UltimateEnergy => ultimateEnergy;
        public AbilityDefinition Ultimate { get; }

        public HeroDefinition(string id, string displayName, HeroClass heroClass, Faction faction,
            int maxHealth, int attack, float attackInterval, int ultimateDamage, int ultimateEnergy = 100,
            AbilityDefinition ultimate = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Hero id is required.", nameof(id));
            if (maxHealth <= 0) throw new ArgumentOutOfRangeException(nameof(maxHealth));
            if (attackInterval <= 0) throw new ArgumentOutOfRangeException(nameof(attackInterval));
            this.id = id;
            this.displayName = displayName;
            this.heroClass = heroClass;
            this.faction = faction;
            this.maxHealth = maxHealth;
            this.attack = Math.Max(0, attack);
            this.attackInterval = attackInterval;
            this.ultimateDamage = Math.Max(0, ultimateDamage);
            this.ultimateEnergy = Math.Max(1, ultimateEnergy);
            Ultimate = ultimate ?? new AbilityDefinition($"{id}_ultimate", this.ultimateDamage);
        }
    }
}
