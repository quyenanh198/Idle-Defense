using System;
using System.Collections.Generic;
using System.Linq;
using IdleHeroDefense.Domain;

namespace IdleHeroDefense.Progression
{
    public readonly struct FormationBonus
    {
        public readonly Faction Faction;
        public readonly int Count;
        public readonly float AttackMultiplier;
        public readonly float HealthMultiplier;
        public FormationBonus(Faction faction, int count, float attackMultiplier, float healthMultiplier)
        {
            Faction = faction; Count = count; AttackMultiplier = attackMultiplier; HealthMultiplier = healthMultiplier;
        }
    }

    public sealed class FormationService
    {
        private readonly PlayerProfile profile;
        public FormationService(PlayerProfile profile) => this.profile = profile ?? throw new ArgumentNullException(nameof(profile));

        public bool TrySet(IEnumerable<string> heroIds, out string reason)
        {
            var ids = heroIds?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? new List<string>();
            if (ids.Count < 1 || ids.Count > 5) { reason = "Formation requires 1-5 heroes."; return false; }
            if (ids.Distinct().Count() != ids.Count) { reason = "A hero can only occupy one slot."; return false; }
            if (ids.Any(id => profile.heroes.All(x => x.heroId != id))) { reason = "Formation contains a locked hero."; return false; }
            profile.activeFormation = ids;
            reason = string.Empty;
            return true;
        }

        public static IReadOnlyList<FormationBonus> CalculateBonuses(IEnumerable<HeroDefinition> heroes)
        {
            return heroes.GroupBy(x => x.Faction).Where(x => x.Count() >= 2).Select(group =>
            {
                var count = group.Count();
                var attack = count >= 5 ? 1.30f : count >= 2 ? 1.10f : 1f;
                var health = count >= 3 ? 1.20f : 1f;
                return new FormationBonus(group.Key, count, attack, health);
            }).ToList();
        }

        public static IReadOnlyList<HeroDefinition> ApplyBonuses(IReadOnlyList<HeroDefinition> heroes)
        {
            var bonuses = CalculateBonuses(heroes).ToDictionary(x => x.Faction);
            return heroes.Select(hero =>
            {
                if (!bonuses.TryGetValue(hero.Faction, out var bonus)) return hero;
                var scaledUltimate = new AbilityDefinition(hero.Ultimate.Id,
                    (int)Math.Round(hero.Ultimate.Damage * bonus.AttackMultiplier), hero.Ultimate.Targeting,
                    hero.Ultimate.StatusEffect);
                return new HeroDefinition(hero.Id, hero.DisplayName, hero.Class, hero.Faction,
                    (int)Math.Round(hero.MaxHealth * bonus.HealthMultiplier),
                    (int)Math.Round(hero.Attack * bonus.AttackMultiplier), hero.AttackInterval,
                    scaledUltimate.Damage, hero.UltimateEnergy, scaledUltimate);
            }).ToList();
        }
    }
}
