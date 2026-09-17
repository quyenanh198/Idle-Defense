using System;
using System.Collections.Generic;
using IdleHeroDefense.Domain;

namespace IdleHeroDefense.Configuration
{
    public static class HeroCatalog
    {
        public static readonly IReadOnlyList<string> AllIds = new[]
        { "ember_knight", "forest_archer", "shade_mage", "iron_guard", "sun_priest", "storm_hunter" };
        private static readonly Dictionary<string, Func<HeroDefinition>> Factories = new Dictionary<string, Func<HeroDefinition>>
        {
            ["ember_knight"] = () => new HeroDefinition("ember_knight", "Ember Knight", HeroClass.Tank, Faction.Light, 450, 34, 1.1f, 150, 100,
                new AbilityDefinition("shield_bash", 150, TargetingRule.FirstEnemy, new StatusEffectDefinition(StatusEffectType.Stun, 2f))),
            ["forest_archer"] = () => new HeroDefinition("forest_archer", "Forest Archer", HeroClass.Ranger, Faction.Nature, 240, 50, 0.8f, 210, 100,
                new AbilityDefinition("finishing_arrow", 210, TargetingRule.LowestHealthEnemy)),
            ["shade_mage"] = () => new HeroDefinition("shade_mage", "Shade Mage", HeroClass.Mage, Faction.Shadow, 210, 62, 1.25f, 120, 100,
                new AbilityDefinition("shadow_flame", 120, TargetingRule.AllEnemies, new StatusEffectDefinition(StatusEffectType.Burn, 4f, 35, 1f))),
            ["iron_guard"] = () => new HeroDefinition("iron_guard", "Iron Guard", HeroClass.Tank, Faction.Machine, 520, 28, 1.2f, 130),
            ["sun_priest"] = () => new HeroDefinition("sun_priest", "Sun Priest", HeroClass.Support, Faction.Light, 260, 42, 1f, 140, 100,
                new AbilityDefinition("solar_wave", 140, TargetingRule.AllEnemies)),
            ["storm_hunter"] = () => new HeroDefinition("storm_hunter", "Storm Hunter", HeroClass.Ranger, Faction.Beast, 280, 68, 0.9f, 280)
        };

        public static HeroDefinition Get(string id)
        {
            if (!Factories.TryGetValue(id, out var factory)) throw new KeyNotFoundException($"Unknown hero: {id}");
            return factory();
        }
    }
}
