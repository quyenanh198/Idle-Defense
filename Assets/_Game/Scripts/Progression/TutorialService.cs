using System;

namespace IdleHeroDefense.Progression
{
    public enum TutorialStep { Welcome, OpenHeroes, UpgradeHero, StartBattle, CompleteBattle, Complete }
    public enum TutorialAction { Continue, HeroesOpened, HeroUpgraded, BattleStarted, BattleWon }

    public sealed class TutorialService
    {
        private readonly PlayerProfile profile;
        public TutorialService(PlayerProfile profile) => this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
        public TutorialStep Current => (TutorialStep)Math.Max(0, Math.Min((int)TutorialStep.Complete, profile.tutorialStep));

        public bool TryAdvance(TutorialAction action)
        {
            var expected = Current == TutorialStep.Welcome ? TutorialAction.Continue
                : Current == TutorialStep.OpenHeroes ? TutorialAction.HeroesOpened
                : Current == TutorialStep.UpgradeHero ? TutorialAction.HeroUpgraded
                : Current == TutorialStep.StartBattle ? TutorialAction.BattleStarted
                : Current == TutorialStep.CompleteBattle ? TutorialAction.BattleWon
                : (TutorialAction)(-1);
            if (action != expected) return false;
            profile.tutorialStep = Math.Min((int)TutorialStep.Complete, profile.tutorialStep + 1);
            return true;
        }
    }

    public enum Feature { Heroes, DailyDungeon, Summon, EndlessTower, Shop }

    public static class FeatureUnlockService
    {
        public static int RequiredStage(Feature feature)
        {
            switch (feature)
            {
                case Feature.DailyDungeon: return 2;
                case Feature.Summon: return 3;
                case Feature.EndlessTower: return 4;
                case Feature.Shop: return 5;
                default: return 1;
            }
        }

        public static bool IsUnlocked(PlayerProfile profile, Feature feature) =>
            profile != null && profile.highestStage >= RequiredStage(feature);
    }
}

