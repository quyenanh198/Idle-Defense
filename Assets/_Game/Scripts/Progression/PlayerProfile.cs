using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleHeroDefense.Progression
{
    [Serializable]
    public sealed class HeroProgress
    {
        public string heroId;
        public int level = 1;
        public int rank = 1;

        public HeroProgress(string heroId) => this.heroId = heroId;
    }

    [Serializable]
    public sealed class HeroShardBalance
    {
        public string heroId;
        public int amount;
        public HeroShardBalance(string heroId, int amount) { this.heroId = heroId; this.amount = amount; }
    }

    [Serializable]
    public sealed class TransactionReceipt
    {
        public string requestId;
        public string fingerprint;
        public bool success;
        public string error;
        public int gold;
        public int gems;
        public TransactionReceipt(string requestId, string fingerprint, bool success, string error, int gold, int gems)
        {
            this.requestId = requestId; this.fingerprint = fingerprint; this.success = success;
            this.error = error; this.gold = gold; this.gems = gems;
        }
    }

    [Serializable]
    public sealed class EquipmentInstance
    {
        public string instanceId;
        public string definitionId;
        public int level = 1;
        public string equippedHeroId = string.Empty;
        public EquipmentInstance(string instanceId, string definitionId) { this.instanceId = instanceId; this.definitionId = definitionId; }
    }

    [Serializable]
    public sealed class ArtifactProgress
    {
        public string artifactId;
        public int level = 1;
        public ArtifactProgress(string artifactId) => this.artifactId = artifactId;
    }

    [Serializable]
    public sealed class PlayerProfile
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string playerId;
        public int playerLevel = 1;
        public int gold;
        public int gems;
        public int highestStage = 1;
        public int totalBattleWins;
        public int summonPity;
        public int serverVersion = 1;
        public int gearMaterials = 200;
        public int artifactDust = 100;
        public int baseLevel = 1;
        public int energy = 60;
        public long lastEnergyUtcTicks;
        public string dailyQuestUtcDate = string.Empty;
        public string weeklyQuestUtcMonday = string.Empty;
        public int dailyBattleWins;
        public int dailyHeroUpgrades;
        public int dailySummons;
        public int weeklyBattleWins;
        public int tutorialStep;
        public bool musicEnabled = true;
        public bool soundEnabled = true;
        public bool reducedMotion;
        public bool showDamageText = true;
        public bool hapticsEnabled = true;
        public string language = "en";
        public string rewardedAdUtcDate = string.Empty;
        public int rewardedAdsWatched;
        public long lastIdleClaimUtcTicks;
        public List<HeroProgress> heroes = new List<HeroProgress>();
        public List<HeroShardBalance> heroShards = new List<HeroShardBalance>();
        public List<string> claimedQuestIds = new List<string>();
        public List<string> claimedMailIds = new List<string>();
        public List<string> activeFormation = new List<string>();
        public List<TransactionReceipt> transactionReceipts = new List<TransactionReceipt>();
        public List<EquipmentInstance> equipment = new List<EquipmentInstance>();
        public List<ArtifactProgress> artifacts = new List<ArtifactProgress>();
        public List<string> processedPurchaseIds = new List<string>();
        public List<string> entitlements = new List<string>();
        public List<string> processedAdTransactionIds = new List<string>();
        public List<string> dailyClaimedQuestIds = new List<string>();
        public List<string> weeklyClaimedQuestIds = new List<string>();
        public List<BackendSummonReceipt> backendSummonReceipts = new List<BackendSummonReceipt>();
        public List<BackendMutationReceipt> backendMutationReceipts = new List<BackendMutationReceipt>();
        public List<BackendRewardReceipt> backendRewardReceipts = new List<BackendRewardReceipt>();
        public List<BackendIdleReceipt> backendIdleReceipts = new List<BackendIdleReceipt>();
        public List<BackendQuestReceipt> backendQuestReceipts = new List<BackendQuestReceipt>();
        public int endlessTowerFloor = 1;
        public string dailyDungeonUtcDate = string.Empty;
        public int dailyDungeonAttemptsUsed;

        public HeroProgress GetOrCreateHero(string heroId)
        {
            var hero = heroes.FirstOrDefault(x => x.heroId == heroId);
            if (hero != null) return hero;
            hero = new HeroProgress(heroId);
            heroes.Add(hero);
            return hero;
        }

        public int GetShards(string heroId)
        {
            var balance = heroShards.Find(x => x.heroId == heroId);
            return balance?.amount ?? 0;
        }

        public void AddShards(string heroId, int amount)
        {
            if (amount <= 0) return;
            var balance = heroShards.Find(x => x.heroId == heroId);
            if (balance == null)
            {
                heroShards.Add(new HeroShardBalance(heroId, amount));
                return;
            }
            balance.amount = checked(balance.amount + amount);
        }

        public void Normalize()
        {
            heroes = heroes ?? new List<HeroProgress>();
            heroShards = heroShards ?? new List<HeroShardBalance>();
            claimedQuestIds = claimedQuestIds ?? new List<string>();
            claimedMailIds = claimedMailIds ?? new List<string>();
            activeFormation = activeFormation ?? new List<string>();
            transactionReceipts = transactionReceipts ?? new List<TransactionReceipt>();
            equipment = equipment ?? new List<EquipmentInstance>();
            artifacts = artifacts ?? new List<ArtifactProgress>();
            processedPurchaseIds = processedPurchaseIds ?? new List<string>();
            entitlements = entitlements ?? new List<string>();
            processedAdTransactionIds = processedAdTransactionIds ?? new List<string>();
            dailyClaimedQuestIds = dailyClaimedQuestIds ?? new List<string>();
            weeklyClaimedQuestIds = weeklyClaimedQuestIds ?? new List<string>();
            backendSummonReceipts = backendSummonReceipts ?? new List<BackendSummonReceipt>();
            backendMutationReceipts = backendMutationReceipts ?? new List<BackendMutationReceipt>();
            backendRewardReceipts = backendRewardReceipts ?? new List<BackendRewardReceipt>();
            backendIdleReceipts = backendIdleReceipts ?? new List<BackendIdleReceipt>();
            backendQuestReceipts = backendQuestReceipts ?? new List<BackendQuestReceipt>();
            highestStage = Math.Max(1, highestStage);
            playerLevel = Math.Max(1, playerLevel);
            serverVersion = Math.Max(1, serverVersion);
            endlessTowerFloor = Math.Max(1, endlessTowerFloor);
            baseLevel = Math.Max(1, baseLevel);
            if (lastEnergyUtcTicks <= 0)
            {
                energy = EnergyService.MaximumEnergy;
                lastEnergyUtcTicks = DateTimeOffset.UtcNow.UtcDateTime.Ticks;
            }
            energy = Math.Max(0, Math.Min(EnergyService.MaximumEnergy, energy));
            if (string.IsNullOrWhiteSpace(language)) language = "en";
        }

        public static PlayerProfile CreateNew(string playerId, DateTimeOffset now)
        {
            var profile = new PlayerProfile
            {
                playerId = playerId,
                gold = 500,
                gems = 100,
                lastIdleClaimUtcTicks = now.UtcDateTime.Ticks
            };
            profile.lastEnergyUtcTicks = now.UtcDateTime.Ticks;
            profile.Normalize();
            return profile;
        }
    }
}
