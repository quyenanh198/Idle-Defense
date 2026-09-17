using System;
using IdleHeroDefense.Infrastructure;

namespace IdleHeroDefense.Progression
{
    public enum GameMode { Campaign, DailyDungeon, EndlessTower }

    public readonly struct ModeReward
    {
        public readonly int Gold;
        public readonly int Gems;
        public readonly int GearMaterials;
        public readonly int ArtifactDust;
        public ModeReward(int gold, int gems, int gearMaterials = 0, int artifactDust = 0)
        { Gold = gold; Gems = gems; GearMaterials = gearMaterials; ArtifactDust = artifactDust; }
    }

    public sealed class GameModeService
    {
        private readonly PlayerProfile profile;
        private readonly IClock clock;
        private readonly LiveConfig config;

        public GameModeService(PlayerProfile profile, IClock clock, LiveConfig config = null)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.config = config ?? LiveConfigService.Current;
        }

        public int DailyAttemptLimit => config.dailyDungeonAttempts;
        public static int EnergyCost(GameMode mode) => mode == GameMode.DailyDungeon ? 5 : mode == GameMode.EndlessTower ? 3 : 0;

        public int DailyAttemptsRemaining
        {
            get { RefreshDaily(); return Math.Max(0, DailyAttemptLimit - profile.dailyDungeonAttemptsUsed); }
        }

        public bool CanStart(GameMode mode, out string reason)
        {
            if (mode == GameMode.DailyDungeon && DailyAttemptsRemaining <= 0)
            {
                reason = "No daily dungeon attempts remaining.";
                return false;
            }
            var cost = EnergyCost(mode);
            if (new EnergyService(profile, clock).Current < cost)
            {
                reason = $"Not enough energy. Requires {cost}.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        public bool TryStart(GameMode mode, out string reason)
        {
            if (!CanStart(mode, out reason)) return false;
            if (!new EnergyService(profile, clock).TrySpend(EnergyCost(mode)))
            { reason = "Not enough energy."; return false; }
            return true;
        }

        public ModeReward CompleteVictory(GameMode mode)
        {
            RefreshDaily();
            ModeReward reward;
            switch (mode)
            {
                case GameMode.DailyDungeon:
                    if (profile.dailyDungeonAttemptsUsed >= DailyAttemptLimit) return new ModeReward();
                    profile.dailyDungeonAttemptsUsed++;
                    reward = new ModeReward(config.dailyDungeonGold, 0, 50);
                    break;
                case GameMode.EndlessTower:
                    reward = new ModeReward(config.towerBaseGold + profile.endlessTowerFloor * config.towerGoldPerFloor,
                        profile.endlessTowerFloor % config.towerGemInterval == 0 ? config.towerGemReward : 0, 0, 25);
                    profile.endlessTowerFloor++;
                    break;
                default:
                    reward = new ModeReward(config.campaignGold, 0);
                    profile.highestStage++;
                    break;
            }
            profile.totalBattleWins++;
            var economy = new EconomyService(profile);
            if (reward.Gold > 0) economy.Grant(CurrencyType.Gold, reward.Gold);
            if (reward.Gems > 0) economy.Grant(CurrencyType.Gems, reward.Gems);
            profile.gearMaterials = checked(profile.gearMaterials + reward.GearMaterials);
            profile.artifactDust = checked(profile.artifactDust + reward.ArtifactDust);
            return reward;
        }

        private void RefreshDaily()
        {
            var today = clock.UtcNow.ToString("yyyy-MM-dd");
            if (profile.dailyDungeonUtcDate == today) return;
            profile.dailyDungeonUtcDate = today;
            profile.dailyDungeonAttemptsUsed = 0;
        }
    }
}
