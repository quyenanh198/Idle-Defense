using System;

namespace IdleHeroDefense.Progression
{
    public sealed class HeroUpgradeService
    {
        public const int MaximumLevel = 60;
        private readonly PlayerProfile profile;
        private readonly EconomyService economy;

        public HeroUpgradeService(PlayerProfile profile)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            economy = new EconomyService(profile);
        }

        public static int GoldCostForNextLevel(int currentLevel) => 50 + Math.Max(0, currentLevel - 1) * 25;

        public EconomyResult LevelUp(string heroId)
        {
            if (string.IsNullOrWhiteSpace(heroId)) return new EconomyResult(false, "Hero id is required.", profile.gold);
            var hero = profile.GetOrCreateHero(heroId);
            if (hero.level >= MaximumLevel) return new EconomyResult(false, "Maximum level reached.", profile.gold);
            var spend = economy.Spend(CurrencyType.Gold, GoldCostForNextLevel(hero.level));
            if (!spend.Success) return spend;
            hero.level++;
            return spend;
        }
    }
}

