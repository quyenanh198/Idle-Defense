using IdleHeroDefense.Domain;
using UnityEngine;

namespace IdleHeroDefense.Configuration
{
    [CreateAssetMenu(menuName = "Idle Hero Defense/Hero", fileName = "Hero_")]
    public sealed class HeroConfig : ScriptableObject
    {
        [SerializeField] private string heroId = "hero";
        [SerializeField] private string displayName = "Hero";
        [SerializeField] private HeroClass heroClass;
        [SerializeField] private Faction faction;
        [Min(1)] [SerializeField] private int maxHealth = 100;
        [Min(0)] [SerializeField] private int attack = 10;
        [Min(0.1f)] [SerializeField] private float attackInterval = 1f;
        [Min(0)] [SerializeField] private int ultimateDamage = 50;
        [Min(1)] [SerializeField] private int ultimateEnergy = 100;

        public string HeroId => heroId;
        public string DisplayName => displayName;

        public HeroDefinition ToDefinition() => new HeroDefinition(heroId, displayName, heroClass, faction,
            maxHealth, attack, attackInterval, ultimateDamage, ultimateEnergy);
    }
}

