using System;
using System.Threading;
using System.Threading.Tasks;
using IdleHeroDefense.Progression;

namespace IdleHeroDefense.Infrastructure
{
    [Serializable]
    public sealed class BackendSession
    {
        public string playerId;
        public string accessToken;
        public long expiresUtcTicks;
        public bool IsValid(DateTimeOffset now) => !string.IsNullOrWhiteSpace(accessToken) && expiresUtcTicks > now.UtcDateTime.Ticks;
    }

    public enum EconomyOperation { Grant, Spend }

    [Serializable]
    public sealed class EconomyTransactionRequest
    {
        public string requestId;
        public EconomyOperation operation;
        public CurrencyType currency;
        public int amount;
        public string reason;

        public string Fingerprint => $"{operation}|{currency}|{amount}|{reason}";
    }

    [Serializable]
    public sealed class EconomyTransactionResponse
    {
        public bool success;
        public string error;
        public int gold;
        public int gems;
        public bool wasReplay;
    }

    [Serializable]
    public sealed class BackendHeroShard
    {
        public string heroId;
        public int amount;
    }

    [Serializable]
    public sealed class BackendLevel { public string id; public int level; }

    [Serializable]
    public sealed class BackendProfileSnapshot
    {
        public string playerId;
        public int version;
        public int gold;
        public int gems;
        public int highestStage;
        public int summonPity;
        public int gearMaterials;
        public int artifactDust;
        public int baseLevel;
        public int endlessTowerFloor;
        public int dailyAttemptsUsed;
        public string dailyAttemptDate;
        public int totalBattleWins;
        public int energy;
        public long lastEnergyUtcTicks;
        public long lastIdleClaimUtcTicks;
        public string dailyQuestDate;
        public string weeklyQuestMonday;
        public int dailyWins;
        public int dailyHeroUpgrades;
        public int dailySummons;
        public int weeklyWins;
        public string[] dailyQuestClaims;
        public string[] weeklyQuestClaims;
        public string[] activeFormation;
        public BackendHeroShard[] heroShards;
        public BackendLevel[] heroLevels;
        public BackendLevel[] equipmentLevels;
        public BackendLevel[] artifactLevels;
        public string[] claimedAchievements;
        public string[] claimedMail;
        public string[] entitlements;
        public int rewardedAdsWatched;
        public string rewardedAdUtcDate;
    }

    [Serializable]
    public sealed class BackendProfileUpdate
    {
        public int expectedVersion;
        public int highestStage;
        public string[] activeFormation;
    }

    [Serializable]
    public sealed class BackendSummonRequest { public string requestId; }

    [Serializable]
    public sealed class BackendSummonResponse
    {
        public bool success;
        public string error;
        public string heroId;
        public string rarity;
        public int shards;
        public bool wasPity;
        public int gems;
        public int summonPity;
    }

    public enum BackendProgressionKind { UpgradeBase, UpgradeArtifact, UpgradeEquipment, ClaimAchievement, ClaimMail }

    [Serializable]
    public sealed class BackendProgressionRequest
    {
        public string requestId;
        public BackendProgressionKind kind;
        public string targetId;
    }

    [Serializable]
    public sealed class BackendProgressionResponse
    {
        public bool success;
        public string error;
        public bool wasReplay;
        public int version;
        public int gold;
        public int gems;
        public int gearMaterials;
        public int artifactDust;
        public int baseLevel;
        public int targetLevel;
    }

    [Serializable] public sealed class BackendBattleStartRequest { public string requestId; public GameMode mode; }
    [Serializable]
    public sealed class BackendBattleStartResponse
    {
        public bool success; public string error; public bool wasReplay; public string ticketId;
        public int energy; public long lastEnergyUtcTicks; public long serverUtcTicks;
    }
    [Serializable] public sealed class BackendTranscriptEvent
    { public int step; public string sourceId; public string targetId; public int amount; public bool isUltimate; }
    [Serializable] public sealed class BackendBattleRequest
    { public string requestId; public string ticketId; public GameMode mode; public int totalSteps; public string transcriptHash; public BackendTranscriptEvent[] events; }
    [Serializable]
    public sealed class BackendRewardResponse
    {
        public bool success; public string error; public bool wasReplay;
        public int version; public int gold; public int gems; public int gearMaterials; public int artifactDust;
        public int highestStage; public int endlessTowerFloor; public int dailyAttemptsUsed; public int totalBattleWins;
        public int rewardGold; public int rewardGems; public int rewardGearMaterials; public int rewardArtifactDust;
    }
    [Serializable] public sealed class BackendIdleRequest { public string requestId; }
    [Serializable]
    public sealed class BackendIdleResponse
    {
        public bool success; public string error; public bool wasReplay; public int version;
        public int gold; public int rewardGold; public long creditedSeconds; public long lastClaimUtcTicks;
    }
    [Serializable] public sealed class BackendQuestClaimRequest { public string requestId; public string questId; }
    [Serializable]
    public sealed class BackendQuestClaimResponse
    {
        public bool success; public string error; public bool wasReplay; public int version;
        public int gold; public int gems; public int rewardGold; public int rewardGems;
    }
    [Serializable] public sealed class BackendAdClaimRequest { public string transactionId; }
    [Serializable]
    public sealed class BackendAdClaimResponse
    {
        public bool success; public string error; public bool wasReplay; public int version;
        public int gems; public int rewardedAdsWatched; public string rewardedAdUtcDate;
    }

    public interface IGameBackend
    {
        Task<BackendSession> AuthenticateGuestAsync(string installId, CancellationToken cancellationToken);
        Task<EconomyTransactionResponse> ExecuteEconomyAsync(BackendSession session,
            EconomyTransactionRequest request, CancellationToken cancellationToken);
        Task<BackendProfileSnapshot> GetProfileAsync(BackendSession session, CancellationToken cancellationToken);
        Task<BackendProfileSnapshot> UpdateProfileAsync(BackendSession session, BackendProfileUpdate update,
            CancellationToken cancellationToken);
        Task<BackendSummonResponse> SummonAsync(BackendSession session, BackendSummonRequest request,
            CancellationToken cancellationToken);
        Task<BackendProgressionResponse> MutateProgressionAsync(BackendSession session,
            BackendProgressionRequest request, CancellationToken cancellationToken);
        Task<BackendRewardResponse> CompleteBattleAsync(BackendSession session, BackendBattleRequest request,
            CancellationToken cancellationToken);
        Task<BackendBattleStartResponse> StartBattleAsync(BackendSession session, BackendBattleStartRequest request,
            CancellationToken cancellationToken);
        Task<BackendIdleResponse> ClaimIdleAsync(BackendSession session, BackendIdleRequest request,
            CancellationToken cancellationToken);
        Task<BackendQuestClaimResponse> ClaimRepeatableQuestAsync(BackendSession session,
            BackendQuestClaimRequest request, CancellationToken cancellationToken);
        Task<BackendAdClaimResponse> ClaimRewardedAdAsync(BackendSession session, BackendAdClaimRequest request,
            CancellationToken cancellationToken);
    }
}
