using System.Collections.Generic;
using System.Linq;
using IdleHeroDefense.Configuration;
using IdleHeroDefense.Domain;
using IdleHeroDefense.Progression;

namespace IdleHeroDefense.Presentation
{
    public static class DemoContent
    {
        public static BattleSimulation CreateBattle(IBattleEventSink eventSink, PlayerProfile profile = null,
            GameMode mode = GameMode.Campaign)
        {
            var ids = profile != null && profile.activeFormation.Count > 0
                ? profile.activeFormation
                : new List<string> { "ember_knight", "forest_archer", "shade_mage" };
            var heroes = ids.Select(id => BuildHero(id, profile)).ToList();
            heroes = FormationService.ApplyBonuses(heroes).ToList();

            var difficulty = 1f;
            if (profile != null)
            {
                if (mode == GameMode.Campaign) difficulty += (profile.highestStage - 1) * 0.08f;
                if (mode == GameMode.EndlessTower) difficulty += (profile.endlessTowerFloor - 1) * 0.12f;
                if (mode == GameMode.DailyDungeon) difficulty = 1.35f;
            }

            var waves = new IReadOnlyList<EnemyDefinition>[]
            {
                new[]
                {
                    Enemy("slime_a", 160, 16, 1.4f, difficulty),
                    Enemy("slime_b", 160, 16, 1.4f, difficulty)
                },
                new[]
                {
                    Enemy("orc_a", 260, 24, 1.2f, difficulty),
                    Enemy("orc_b", 260, 24, 1.2f, difficulty)
                },
                new[] { Enemy("stone_golem", 900, 42, 1.5f, difficulty, true) }
            };
            var baseHealth = profile == null ? 500 : (int)(new MetaProgressionService(profile).BaseMaxHealth *
                new MetaProgressionService(profile).BaseHealthMultiplier);
            return new BattleSimulation(heroes, waves, baseHealth, eventSink);
        }

        private static HeroDefinition BuildHero(string id, PlayerProfile profile)
        {
            var hero = ScaleForLevel(HeroCatalog.Get(id), profile?.GetOrCreateHero(id).level ?? 1);
            if (profile == null) return hero;
            var equipment = new EquipmentService(profile).StatsFor(id);
            var attackMultiplier = new MetaProgressionService(profile).HeroAttackMultiplier;
            var attack = (int)((hero.Attack + equipment.attack) * attackMultiplier);
            var ultimate = new AbilityDefinition(hero.Ultimate.Id,
                (int)((hero.Ultimate.Damage + equipment.attack) * attackMultiplier), hero.Ultimate.Targeting, hero.Ultimate.StatusEffect);
            return new HeroDefinition(hero.Id, hero.DisplayName, hero.Class, hero.Faction,
                hero.MaxHealth + equipment.health, attack, hero.AttackInterval, ultimate.Damage, hero.UltimateEnergy, ultimate);
        }

        private static HeroDefinition ScaleForLevel(HeroDefinition hero, int level)
        {
            var multiplier = 1f + (level - 1) * 0.08f;
            var ultimate = new AbilityDefinition(hero.Ultimate.Id, (int)(hero.Ultimate.Damage * multiplier),
                hero.Ultimate.Targeting, hero.Ultimate.StatusEffect);
            return new HeroDefinition(hero.Id, hero.DisplayName, hero.Class, hero.Faction,
                (int)(hero.MaxHealth * multiplier), (int)(hero.Attack * multiplier), hero.AttackInterval,
                ultimate.Damage, hero.UltimateEnergy, ultimate);
        }

        private static EnemyDefinition Enemy(string id, int health, int attack, float interval, float scale, bool boss = false) =>
            new EnemyDefinition(id, (int)(health * scale), (int)(attack * scale), interval, boss);
    }
}
