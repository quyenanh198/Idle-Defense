using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using IdleHeroDefense.Infrastructure;
using IdleHeroDefense.Progression;
using UnityEngine;

namespace IdleHeroDefense.Presentation
{
    public sealed class BackendController : MonoBehaviour
    {
        private const string InstallIdKey = "idle_hero_defense.install_id";
        private const string ApiUrlKey = "idle_hero_defense.api_url";
        private CancellationTokenSource lifetime;
        private IGameBackend backend;
        private BackendSession session;
        private bool online;
        private bool syncing;
        private bool syncRequested;
        private string activeBattleTicket = string.Empty;

        public bool IsReady => session != null && session.IsValid(DateTimeOffset.UtcNow);
        public bool IsOnline => online;
        public string Mode { get; private set; } = "Initializing";
        public string LastError { get; private set; } = string.Empty;
        public string SummonStatus { get; private set; } = string.Empty;
        public bool IsSummoning { get; private set; }
        public bool IsEconomyBusy { get; private set; }
        public string EconomyStatus { get; private set; } = string.Empty;
        public bool IsProgressionBusy { get; private set; }
        public string ProgressionStatus { get; private set; } = string.Empty;
        public string RewardStatus { get; private set; } = string.Empty;
        public bool IsRewardBusy { get; private set; }

        private async void Start()
        {
            lifetime = new CancellationTokenSource();
            var profileController = ProfileController.Instance;
            if (profileController == null) { LastError = "Profile is unavailable."; return; }
            var apiUrl = PlayerPrefs.GetString(ApiUrlKey, string.Empty);
            online = !string.IsNullOrWhiteSpace(apiUrl);
            backend = string.IsNullOrWhiteSpace(apiUrl)
                ? (IGameBackend)new OfflineGameBackend(profileController.Profile, new SystemClock())
                : new UnityWebRequestBackend(apiUrl);
            Mode = string.IsNullOrWhiteSpace(apiUrl) ? "Offline authoritative" : "Online authoritative";
            try
            {
                session = await backend.AuthenticateGuestAsync(GetOrCreateInstallId(), lifetime.Token);
                var snapshot = await backend.GetProfileAsync(session, lifetime.Token);
                ApplySnapshot(snapshot, online);
                AnalyticsService.Track("authentication_completed", "mode", Mode, "player_id", session.playerId);
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                Debug.LogError($"Authentication failed: {exception.Message}");
            }
        }

        public async Task<EconomyTransactionResponse> ExecuteEconomyAsync(EconomyOperation operation,
            CurrencyType currency, int amount, string reason, string requestId = null)
        {
            if (!IsReady) throw new InvalidOperationException("Backend session is not ready.");
            var request = new EconomyTransactionRequest
            {
                requestId = string.IsNullOrWhiteSpace(requestId) ? Guid.NewGuid().ToString("N") : requestId,
                operation = operation,
                currency = currency,
                amount = amount,
                reason = reason ?? string.Empty
            };
            var response = await backend.ExecuteEconomyAsync(session, request, lifetime.Token);
            if (response.success)
            {
                var profile = ProfileController.Instance.Profile;
                profile.gold = response.gold;
                profile.gems = response.gems;
                ProfileController.Instance?.Save();
                AnalyticsService.Track("currency_transaction", "operation", operation, "currency", currency,
                    "amount", amount, "reason", reason, "replay", response.wasReplay);
            }
            return response;
        }

        public async void SummonOne()
        {
            if (!IsReady || IsSummoning) { SummonStatus = "Backend is not ready."; return; }
            IsSummoning = true;
            var requestId = Guid.NewGuid().ToString("N");
            try
            {
                var result = await backend.SummonAsync(session, new BackendSummonRequest { requestId = requestId }, lifetime.Token);
                if (!result.success) { SummonStatus = result.error; return; }
                var profile = ProfileController.Instance.Profile;
                profile.gems = result.gems;
                profile.summonPity = result.summonPity;
                if (online) profile.AddShards(result.heroId, result.shards);
                profile.GetOrCreateHero(result.heroId);
                new RepeatableQuestService(profile, new SystemClock()).RecordSummon();
                ProfileController.Instance.Save();
                SummonStatus = $"{result.rarity}: {result.heroId} +{result.shards} shards";
                AnalyticsService.Track("summon_result", "hero_id", result.heroId, "rarity", result.rarity,
                    "pity", result.wasPity, "gems_after", result.gems, "authority", Mode);
            }
            catch (Exception exception)
            {
                LastError = exception.Message;
                SummonStatus = exception.Message;
            }
            finally { IsSummoning = false; }
        }

        public async void LevelUpHero(string heroId)
        {
            if (!IsReady || IsEconomyBusy) { EconomyStatus = "Backend is not ready."; return; }
            var profile = ProfileController.Instance.Profile;
            var hero = profile.GetOrCreateHero(heroId);
            if (hero.level >= HeroUpgradeService.MaximumLevel) { EconomyStatus = "Maximum level reached."; return; }
            var cost = HeroUpgradeService.GoldCostForNextLevel(hero.level);
            IsEconomyBusy = true;
            try
            {
                var result = await ExecuteEconomyAsync(EconomyOperation.Spend, CurrencyType.Gold, cost,
                    $"hero_level:{heroId}:{hero.level + 1}");
                if (!result.success) { EconomyStatus = result.error; return; }
                hero.level++;
                new RepeatableQuestService(profile, new SystemClock()).RecordHeroUpgrade();
                ProfileController.Instance.Save();
                ProfileController.Instance.AdvanceTutorial(TutorialAction.HeroUpgraded);
                EconomyStatus = $"{heroId} reached level {hero.level}";
            }
            catch (Exception exception) { LastError = exception.Message; EconomyStatus = exception.Message; }
            finally { IsEconomyBusy = false; }
        }

        public void RequestProfileSync()
        {
            if (!online || !IsReady) return;
            syncRequested = true;
            if (!syncing) _ = SynchronizeLoopAsync();
        }

        public async Task RefreshProfileAsync()
        {
            if (!IsReady) throw new InvalidOperationException("Backend session is not ready.");
            var snapshot = await backend.GetProfileAsync(session, lifetime.Token);
            ApplySnapshot(snapshot, online);
        }

        public async Task<BackendAdClaimResponse> ClaimRewardedAdAsync(string transactionId)
        {
            if (!IsReady) throw new InvalidOperationException("Backend session is not ready.");
            var response = await backend.ClaimRewardedAdAsync(session,
                new BackendAdClaimRequest { transactionId = transactionId }, lifetime.Token);
            if (response.success)
            {
                var profile = ProfileController.Instance.Profile;
                profile.serverVersion = Math.Max(profile.serverVersion, response.version);
                profile.gems = response.gems; profile.rewardedAdsWatched = response.rewardedAdsWatched;
                profile.rewardedAdUtcDate = response.rewardedAdUtcDate;
                ProfileController.Instance.Save();
            }
            return response;
        }

        public void UpgradeBase() => MutateProgression(BackendProgressionKind.UpgradeBase, string.Empty);
        public void UpgradeArtifact(string artifactId) => MutateProgression(BackendProgressionKind.UpgradeArtifact, artifactId);
        public void UpgradeEquipment(string instanceId) => MutateProgression(BackendProgressionKind.UpgradeEquipment, instanceId);
        public void ClaimAchievement(string questId) => MutateProgression(BackendProgressionKind.ClaimAchievement, questId);
        public void ClaimMail(string mailId) => MutateProgression(BackendProgressionKind.ClaimMail, mailId);

        public async void CompleteBattle(GameMode mode)
        {
            if (!IsReady || IsRewardBusy) { RewardStatus = "Backend is not ready."; return; }
            IsRewardBusy = true;
            try
            {
                var recorded = FindObjectOfType<BattleRunner>()?.Transcript;
                var events = recorded?.Events.Select(x => new BackendTranscriptEvent
                {
                    step = x.step, sourceId = x.sourceId, targetId = x.targetId, amount = x.amount, isUltimate = x.isUltimate
                }).ToArray() ?? Array.Empty<BackendTranscriptEvent>();
                var response = await backend.CompleteBattleAsync(session, new BackendBattleRequest
                {
                    requestId = Guid.NewGuid().ToString("N"), ticketId = activeBattleTicket, mode = mode,
                    totalSteps = recorded?.TotalSteps ?? 0, transcriptHash = recorded?.ComputeHash() ?? string.Empty, events = events
                }, lifetime.Token);
                if (!response.success) { RewardStatus = response.error; return; }
                var profile = ProfileController.Instance.Profile;
                ApplyRewardState(profile, response);
                if (online) new RepeatableQuestService(profile, new SystemClock()).RecordBattleWin();
                ProfileController.Instance.Save();
                RewardStatus = $"+{response.rewardGold} gold" + (response.rewardGems > 0 ? $" +{response.rewardGems} gems" : string.Empty);
                AnalyticsService.Track("stage_completed", "mode", mode, "stage", response.highestStage,
                    "gold", response.rewardGold, "gems", response.rewardGems, "authority", Mode);
            }
            catch (Exception exception) { LastError = exception.Message; RewardStatus = exception.Message; }
            finally { activeBattleTicket = string.Empty; IsRewardBusy = false; }
        }

        public async void StartBattle(GameMode mode)
        {
            if (!IsReady || IsRewardBusy) { RewardStatus = "Backend is not ready."; return; }
            IsRewardBusy = true;
            try
            {
                var response = await backend.StartBattleAsync(session,
                    new BackendBattleStartRequest { requestId = Guid.NewGuid().ToString("N"), mode = mode }, lifetime.Token);
                if (!response.success) { RewardStatus = response.error; return; }
                var profile = ProfileController.Instance.Profile;
                profile.energy = response.energy; profile.lastEnergyUtcTicks = response.lastEnergyUtcTicks;
                ProfileController.Instance.Save();
                activeBattleTicket = response.ticketId;
                FindObjectOfType<BattleRunner>()?.StartAuthorizedMode(mode);
                ProfileController.Instance.AdvanceTutorial(TutorialAction.BattleStarted);
                RewardStatus = string.Empty;
            }
            catch (Exception exception) { LastError = exception.Message; RewardStatus = exception.Message; }
            finally { IsRewardBusy = false; }
        }

        public async void ClaimIdleReward()
        {
            if (!IsReady || IsRewardBusy) { RewardStatus = "Backend is not ready."; return; }
            IsRewardBusy = true;
            try
            {
                var response = await backend.ClaimIdleAsync(session,
                    new BackendIdleRequest { requestId = Guid.NewGuid().ToString("N") }, lifetime.Token);
                if (!response.success) { RewardStatus = response.error; return; }
                var profile = ProfileController.Instance.Profile;
                profile.serverVersion = Math.Max(profile.serverVersion, response.version);
                profile.gold = response.gold; profile.lastIdleClaimUtcTicks = response.lastClaimUtcTicks;
                ProfileController.Instance.Save();
                RewardStatus = $"Claimed {response.rewardGold} gold";
            }
            catch (Exception exception) { LastError = exception.Message; RewardStatus = exception.Message; }
            finally { IsRewardBusy = false; }
        }

        public async void ClaimRepeatableQuest(string questId)
        {
            if (!IsReady || IsRewardBusy) { RewardStatus = "Backend is not ready."; return; }
            IsRewardBusy = true;
            try
            {
                var response = await backend.ClaimRepeatableQuestAsync(session,
                    new BackendQuestClaimRequest { requestId = Guid.NewGuid().ToString("N"), questId = questId }, lifetime.Token);
                if (!response.success) { RewardStatus = response.error; return; }
                var profile = ProfileController.Instance.Profile;
                profile.serverVersion = Math.Max(profile.serverVersion, response.version);
                profile.gold = response.gold; profile.gems = response.gems;
                var claims = questId.StartsWith("daily_", StringComparison.Ordinal)
                    ? profile.dailyClaimedQuestIds : profile.weeklyClaimedQuestIds;
                if (!claims.Contains(questId)) claims.Add(questId);
                ProfileController.Instance.Save();
                RewardStatus = "Quest reward claimed";
            }
            catch (Exception exception) { LastError = exception.Message; RewardStatus = exception.Message; }
            finally { IsRewardBusy = false; }
        }

        private static void ApplyRewardState(PlayerProfile profile, BackendRewardResponse response)
        {
            profile.serverVersion = Math.Max(profile.serverVersion, response.version);
            profile.gold = response.gold; profile.gems = response.gems;
            profile.gearMaterials = response.gearMaterials; profile.artifactDust = response.artifactDust;
            profile.highestStage = response.highestStage; profile.endlessTowerFloor = response.endlessTowerFloor;
            profile.dailyDungeonAttemptsUsed = response.dailyAttemptsUsed; profile.totalBattleWins = response.totalBattleWins;
        }

        private async void MutateProgression(BackendProgressionKind kind, string targetId)
        {
            if (!IsReady || IsProgressionBusy) { ProgressionStatus = "Backend is not ready."; return; }
            IsProgressionBusy = true;
            try
            {
                var response = await backend.MutateProgressionAsync(session, new BackendProgressionRequest
                {
                    requestId = Guid.NewGuid().ToString("N"), kind = kind, targetId = targetId ?? string.Empty
                }, lifetime.Token);
                if (!response.success) { ProgressionStatus = response.error; return; }
                var profile = ProfileController.Instance.Profile;
                profile.serverVersion = Math.Max(profile.serverVersion, response.version);
                profile.gold = response.gold; profile.gems = response.gems;
                profile.gearMaterials = response.gearMaterials; profile.artifactDust = response.artifactDust;
                profile.baseLevel = response.baseLevel;
                if (online)
                {
                    if (kind == BackendProgressionKind.UpgradeArtifact)
                    {
                        var artifact = profile.artifacts.FirstOrDefault(x => x.artifactId == targetId);
                        if (artifact == null) { artifact = new ArtifactProgress(targetId); profile.artifacts.Add(artifact); }
                        artifact.level = response.targetLevel;
                    }
                    else if (kind == BackendProgressionKind.UpgradeEquipment)
                    {
                        var item = profile.equipment.FirstOrDefault(x => x.instanceId == targetId);
                        if (item != null) item.level = response.targetLevel;
                    }
                    else if (kind == BackendProgressionKind.ClaimAchievement && !profile.claimedQuestIds.Contains(targetId))
                        profile.claimedQuestIds.Add(targetId);
                    else if (kind == BackendProgressionKind.ClaimMail && !profile.claimedMailIds.Contains(targetId))
                        profile.claimedMailIds.Add(targetId);
                }
                ProfileController.Instance.Save();
                ProgressionStatus = response.wasReplay ? "Already applied" : "Progression updated";
                AnalyticsService.Track("progression_mutation", "kind", kind, "target", targetId, "replay", response.wasReplay);
            }
            catch (Exception exception) { LastError = exception.Message; ProgressionStatus = exception.Message; }
            finally { IsProgressionBusy = false; }
        }

        private async Task SynchronizeLoopAsync()
        {
            syncing = true;
            try
            {
                while (syncRequested && IsReady)
                {
                    syncRequested = false;
                    var profile = ProfileController.Instance.Profile;
                    var update = new BackendProfileUpdate
                    {
                        expectedVersion = profile.serverVersion,
                        highestStage = profile.highestStage,
                        activeFormation = profile.activeFormation.ToArray()
                    };
                    try
                    {
                        var snapshot = await backend.UpdateProfileAsync(session, update, lifetime.Token);
                        ApplySnapshot(snapshot, true);
                    }
                    catch (Exception exception)
                    {
                        LastError = exception.Message;
                        var authoritative = await backend.GetProfileAsync(session, lifetime.Token);
                        ApplySnapshot(authoritative, true);
                    }
                }
            }
            catch (Exception exception) { LastError = exception.Message; }
            finally { syncing = false; }
        }

        private static void ApplySnapshot(BackendProfileSnapshot snapshot, bool replaceAuthoritativeValues)
        {
            if (snapshot == null || ProfileController.Instance == null) return;
            var profile = ProfileController.Instance.Profile;
            profile.serverVersion = Math.Max(1, snapshot.version);
            if (replaceAuthoritativeValues)
            {
                profile.playerId = snapshot.playerId;
                profile.gold = snapshot.gold;
                profile.gems = snapshot.gems;
                profile.highestStage = Math.Max(1, snapshot.highestStage);
                profile.summonPity = snapshot.summonPity;
                profile.gearMaterials = snapshot.gearMaterials;
                profile.artifactDust = snapshot.artifactDust;
                profile.baseLevel = Math.Max(1, snapshot.baseLevel);
                profile.endlessTowerFloor = Math.Max(1, snapshot.endlessTowerFloor);
                profile.dailyDungeonAttemptsUsed = snapshot.dailyAttemptsUsed;
                profile.dailyDungeonUtcDate = snapshot.dailyAttemptDate ?? string.Empty;
                profile.totalBattleWins = snapshot.totalBattleWins;
                profile.energy = snapshot.energy;
                profile.lastEnergyUtcTicks = snapshot.lastEnergyUtcTicks;
                profile.lastIdleClaimUtcTicks = snapshot.lastIdleClaimUtcTicks;
                profile.dailyQuestUtcDate = snapshot.dailyQuestDate ?? string.Empty;
                profile.weeklyQuestUtcMonday = snapshot.weeklyQuestMonday ?? string.Empty;
                profile.dailyBattleWins = snapshot.dailyWins;
                profile.dailyHeroUpgrades = snapshot.dailyHeroUpgrades;
                profile.dailySummons = snapshot.dailySummons;
                profile.weeklyBattleWins = snapshot.weeklyWins;
                profile.dailyClaimedQuestIds = snapshot.dailyQuestClaims == null
                    ? new System.Collections.Generic.List<string>() : new System.Collections.Generic.List<string>(snapshot.dailyQuestClaims);
                profile.weeklyClaimedQuestIds = snapshot.weeklyQuestClaims == null
                    ? new System.Collections.Generic.List<string>() : new System.Collections.Generic.List<string>(snapshot.weeklyQuestClaims);
                profile.heroShards.Clear();
                if (snapshot.heroShards != null)
                    foreach (var shard in snapshot.heroShards)
                    {
                        profile.AddShards(shard.heroId, shard.amount);
                        profile.GetOrCreateHero(shard.heroId);
                    }
                if (snapshot.activeFormation != null && snapshot.activeFormation.Length > 0)
                {
                    foreach (var id in snapshot.activeFormation) profile.GetOrCreateHero(id);
                    profile.activeFormation = new System.Collections.Generic.List<string>(snapshot.activeFormation);
                }
                if (snapshot.heroLevels != null)
                    foreach (var row in snapshot.heroLevels) profile.GetOrCreateHero(row.id).level = Math.Max(1, row.level);
                if (snapshot.equipmentLevels != null)
                    foreach (var row in snapshot.equipmentLevels)
                    {
                        var item = profile.equipment.FirstOrDefault(x => x.instanceId == row.id);
                        if (item != null) item.level = Math.Max(1, row.level);
                    }
                if (snapshot.artifactLevels != null)
                    foreach (var row in snapshot.artifactLevels)
                    {
                        var artifact = profile.artifacts.FirstOrDefault(x => x.artifactId == row.id);
                        if (artifact == null) { artifact = new ArtifactProgress(row.id); profile.artifacts.Add(artifact); }
                        artifact.level = Math.Max(1, row.level);
                    }
                profile.claimedQuestIds = snapshot.claimedAchievements == null
                    ? new System.Collections.Generic.List<string>() : new System.Collections.Generic.List<string>(snapshot.claimedAchievements);
                profile.claimedMailIds = snapshot.claimedMail == null
                    ? new System.Collections.Generic.List<string>() : new System.Collections.Generic.List<string>(snapshot.claimedMail);
                profile.entitlements = snapshot.entitlements == null
                    ? new System.Collections.Generic.List<string>() : new System.Collections.Generic.List<string>(snapshot.entitlements);
                profile.rewardedAdsWatched = snapshot.rewardedAdsWatched;
                profile.rewardedAdUtcDate = snapshot.rewardedAdUtcDate ?? string.Empty;
            }
            ProfileController.Instance.Save();
        }

        private static string GetOrCreateInstallId()
        {
            var id = PlayerPrefs.GetString(InstallIdKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(id)) return id;
            id = Guid.NewGuid().ToString("N");
            PlayerPrefs.SetString(InstallIdKey, id);
            PlayerPrefs.Save();
            return id;
        }

        private void OnDestroy()
        {
            lifetime?.Cancel();
            lifetime?.Dispose();
        }
    }
}
