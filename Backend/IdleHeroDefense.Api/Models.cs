using System.Text.Json.Serialization;

namespace IdleHeroDefense.Api;

public sealed record GuestRequest(string InstallId);
public sealed record SessionResponse(string PlayerId, string AccessToken, long ExpiresUtcTicks);
public sealed record EconomyRequest(string RequestId, int Operation, int Currency, int Amount, string Reason);
public sealed record EconomyResponse(bool Success, string Error, int Gold, int Gems, bool WasReplay = false);
public sealed record SummonRequest(string RequestId);
public sealed record SummonResponse(bool Success, string Error, string HeroId, string Rarity, int Shards, bool WasPity, int Gems, int SummonPity);
public sealed record ReceiptRequest(string ProductId, string TransactionId, string Receipt, string Store);
public sealed record ValidationResponse(bool Valid, string Error, string TransactionId);
public sealed record ProfileUpdateRequest(int ExpectedVersion, int HighestStage, IReadOnlyList<string> ActiveFormation);
public sealed record HeroShardResponse(string HeroId, int Amount);
public sealed record LevelResponse(string Id, int Level);
public sealed record ProfileResponse(string PlayerId, int Version, int Gold, int Gems, int HighestStage, int SummonPity,
    int GearMaterials, int ArtifactDust, int BaseLevel, int EndlessTowerFloor, int DailyAttemptsUsed, string DailyAttemptDate, int TotalBattleWins,
    int Energy, long LastEnergyUtcTicks, long LastIdleClaimUtcTicks, string DailyQuestDate, string WeeklyQuestMonday, int DailyWins,
    int DailyHeroUpgrades, int DailySummons, int WeeklyWins, IReadOnlyList<string> DailyQuestClaims,
    IReadOnlyList<string> WeeklyQuestClaims, IReadOnlyList<string> ActiveFormation,
    IReadOnlyList<HeroShardResponse> HeroShards, IReadOnlyList<LevelResponse> HeroLevels,
    IReadOnlyList<LevelResponse> EquipmentLevels, IReadOnlyList<LevelResponse> ArtifactLevels,
    IReadOnlyList<string> ClaimedAchievements, IReadOnlyList<string> ClaimedMail, IReadOnlyList<string> Entitlements,
    int RewardedAdsWatched, string RewardedAdUtcDate);
public sealed record AnalyticsProperty(string Key, string Value);
public sealed record AnalyticsEvent(string Id, string Name, string SessionId, long OccurredUtcTicks, IReadOnlyList<AnalyticsProperty> Properties);
public sealed record AnalyticsBatch(IReadOnlyList<AnalyticsEvent> Events);
public sealed record ProgressionRequest(string RequestId, int Kind, string TargetId);
public sealed record ProgressionResponse(bool Success, string Error, bool WasReplay, int Version, int Gold, int Gems,
    int GearMaterials, int ArtifactDust, int BaseLevel, int TargetLevel);
public sealed record ProgressionReceipt(string Fingerprint, ProgressionResponse Response);
public sealed record BattleStartRequest(string RequestId, int Mode);
public sealed record BattleStartResponse(bool Success, string Error, bool WasReplay, string TicketId, int Energy,
    long LastEnergyUtcTicks, long ServerUtcTicks);
public sealed record BattleStartReceipt(int Mode, BattleStartResponse Response);
public sealed record TranscriptEvent(int Step, string SourceId, string TargetId, int Amount, bool IsUltimate);
public sealed record BattleRewardRequest(string RequestId, string TicketId, int Mode, int TotalSteps,
    string TranscriptHash, IReadOnlyList<TranscriptEvent> Events);
public sealed record BattleRewardResponse(bool Success, string Error, bool WasReplay, int Version, int Gold, int Gems,
    int GearMaterials, int ArtifactDust, int HighestStage, int EndlessTowerFloor, int DailyAttemptsUsed, int TotalBattleWins,
    int RewardGold, int RewardGems, int RewardGearMaterials, int RewardArtifactDust);
public sealed record BattleReceipt(int Mode, BattleRewardResponse Response);
public sealed record BattleTicket(string PlayerId, int Mode, long ExpiresUtcTicks, bool Consumed,
    IReadOnlyList<string> AllowedHeroes, int RequiredEnemyDamage);
public sealed record IdleRewardRequest(string RequestId);
public sealed record IdleRewardResponse(bool Success, string Error, bool WasReplay, int Version, int Gold,
    int RewardGold, long CreditedSeconds, long LastClaimUtcTicks);
public sealed record QuestClaimRequest(string RequestId, string QuestId);
public sealed record QuestClaimResponse(bool Success, string Error, bool WasReplay, int Version, int Gold, int Gems,
    int RewardGold, int RewardGems);
public sealed record QuestReceipt(string QuestId, QuestClaimResponse Response);
public sealed record AdCallbackRequest(string PlayerId, string TransactionId, string Placement, long ExpiresUtcTicks, string Signature);
public sealed record AdClaimRequest(string TransactionId);
public sealed record AdClaimResponse(bool Success, string Error, bool WasReplay, int Version, int Gems,
    int RewardedAdsWatched, string RewardedAdUtcDate);
public sealed record VerifiedAd(string PlayerId, string Placement, long ExpiresUtcTicks);

public sealed class PlayerState
{
    public string PlayerId { get; set; } = string.Empty;
    public string InstallId { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public int Gold { get; set; } = 500;
    public int Gems { get; set; } = 100;
    public int HighestStage { get; set; } = 1;
    public int SummonPity { get; set; }
    public int GearMaterials { get; set; } = 200;
    public int ArtifactDust { get; set; } = 100;
    public int BaseLevel { get; set; } = 1;
    public int TotalWins { get; set; }
    public int Energy { get; set; } = 60;
    public long LastEnergyUtcTicks { get; set; } = DateTimeOffset.UtcNow.UtcDateTime.Ticks;
    public int EndlessTowerFloor { get; set; } = 1;
    public int DailyAttemptsUsed { get; set; }
    public string DailyAttemptDate { get; set; } = string.Empty;
    public long LastIdleClaimUtcTicks { get; set; } = DateTimeOffset.UtcNow.UtcDateTime.Ticks;
    public string DailyQuestDate { get; set; } = string.Empty;
    public string WeeklyQuestMonday { get; set; } = string.Empty;
    public int DailyWins { get; set; }
    public int DailyHeroUpgrades { get; set; }
    public int DailySummons { get; set; }
    public int WeeklyWins { get; set; }
    public HashSet<string> DailyQuestClaims { get; set; } = [];
    public HashSet<string> WeeklyQuestClaims { get; set; } = [];
    public List<string> ActiveFormation { get; set; } = ["ember_knight", "forest_archer", "shade_mage"];
    public Dictionary<string, int> HeroShards { get; set; } = new();
    public Dictionary<string, EconomyReceipt> EconomyReceipts { get; set; } = new();
    public Dictionary<string, SummonResponse> SummonReceipts { get; set; } = new();
    public HashSet<string> PurchaseTransactions { get; set; } = [];
    public HashSet<string> Entitlements { get; set; } = [];
    public HashSet<string> ProcessedAdTransactions { get; set; } = [];
    public int RewardedAdsWatched { get; set; }
    public string RewardedAdUtcDate { get; set; } = string.Empty;
    public Dictionary<string, int> HeroLevels { get; set; } = new();
    public Dictionary<string, int> EquipmentLevels { get; set; } = new()
    { ["eq_sword"] = 1, ["eq_bow"] = 1, ["eq_staff"] = 1, ["eq_plate"] = 1, ["eq_leathers"] = 1, ["eq_robe"] = 1 };
    public Dictionary<string, int> ArtifactLevels { get; set; } = new() { ["war_banner"] = 1, ["guardian_idol"] = 1 };
    public HashSet<string> ClaimedAchievements { get; set; } = [];
    public HashSet<string> ClaimedMail { get; set; } = [];
    public Dictionary<string, ProgressionReceipt> ProgressionReceipts { get; set; } = new();
    public Dictionary<string, BattleReceipt> BattleReceipts { get; set; } = new();
    public Dictionary<string, BattleStartReceipt> BattleStartReceipts { get; set; } = new();
    public Dictionary<string, IdleRewardResponse> IdleReceipts { get; set; } = new();
    public Dictionary<string, QuestReceipt> QuestReceipts { get; set; } = new();
}

public sealed class EconomyReceipt
{
    public string Fingerprint { get; set; } = string.Empty;
    public EconomyResponse Response { get; set; } = new(false, string.Empty, 0, 0);
}

public sealed class SessionState
{
    public string TokenHash { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public long ExpiresUtcTicks { get; set; }
}

public sealed class StoreState
{
    public Dictionary<string, PlayerState> Players { get; set; } = new();
    public Dictionary<string, string> InstallToPlayer { get; set; } = new();
    public Dictionary<string, SessionState> Sessions { get; set; } = new();
    public HashSet<string> AnalyticsEventIds { get; set; } = [];
    public Dictionary<string, BattleTicket> BattleTickets { get; set; } = new();
    public Dictionary<string, VerifiedAd> VerifiedAds { get; set; } = new();
}
