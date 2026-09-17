using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using IdleHeroDefense.Progression;
using IdleHeroDefense.Monetization;

namespace IdleHeroDefense.Infrastructure
{
    public sealed class OfflineGameBackend : IGameBackend
    {
        private readonly PlayerProfile profile;
        private readonly IClock clock;
        private readonly Dictionary<string, BackendBattleStartResponse> battleStarts = new Dictionary<string, BackendBattleStartResponse>();
        private readonly Dictionary<string, GameMode> battleTickets = new Dictionary<string, GameMode>();

        public OfflineGameBackend(PlayerProfile profile, IClock clock)
        { this.profile = profile ?? throw new ArgumentNullException(nameof(profile)); this.clock = clock ?? throw new ArgumentNullException(nameof(clock)); }

        public Task<BackendSession> AuthenticateGuestAsync(string installId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(installId)) throw new ArgumentException("Install id is required.", nameof(installId));
            return Task.FromResult(new BackendSession
            {
                playerId = profile.playerId,
                accessToken = $"offline_{profile.playerId}",
                expiresUtcTicks = clock.UtcNow.AddDays(30).UtcDateTime.Ticks
            });
        }

        public Task<EconomyTransactionResponse> ExecuteEconomyAsync(BackendSession session,
            EconomyTransactionRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (session == null || !session.IsValid(clock.UtcNow)) return Task.FromResult(Failure("Session is invalid."));
            if (request == null || string.IsNullOrWhiteSpace(request.requestId)) return Task.FromResult(Failure("Request id is required."));

            var existing = profile.transactionReceipts.FirstOrDefault(x => x.requestId == request.requestId);
            if (existing != null)
            {
                if (existing.fingerprint != request.Fingerprint) return Task.FromResult(Failure("Idempotency key collision."));
                return Task.FromResult(new EconomyTransactionResponse
                {
                    success = existing.success, error = existing.error, gold = existing.gold,
                    gems = existing.gems, wasReplay = true
                });
            }

            EconomyResult result;
            var economy = new EconomyService(profile);
            result = request.operation == EconomyOperation.Grant
                ? economy.Grant(request.currency, request.amount)
                : economy.Spend(request.currency, request.amount);
            var response = new EconomyTransactionResponse
            {
                success = result.Success, error = result.Reason, gold = profile.gold, gems = profile.gems
            };
            profile.transactionReceipts.Add(new TransactionReceipt(request.requestId, request.Fingerprint,
                response.success, response.error, response.gold, response.gems));
            if (profile.transactionReceipts.Count > 200) profile.transactionReceipts.RemoveAt(0);
            return Task.FromResult(response);
        }

        private EconomyTransactionResponse Failure(string error) => new EconomyTransactionResponse
        { success = false, error = error, gold = profile.gold, gems = profile.gems };

        public Task<BackendProfileSnapshot> GetProfileAsync(BackendSession session, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (session == null || !session.IsValid(clock.UtcNow)) throw new InvalidOperationException("Session is invalid.");
            return Task.FromResult(ToSnapshot());
        }

        public Task<BackendProfileSnapshot> UpdateProfileAsync(BackendSession session, BackendProfileUpdate update,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (session == null || !session.IsValid(clock.UtcNow)) throw new InvalidOperationException("Session is invalid.");
            if (update == null || update.expectedVersion != profile.serverVersion) throw new InvalidOperationException("Profile version conflict.");
            if (!new FormationService(profile).TrySet(update.activeFormation, out var reason)) throw new InvalidOperationException(reason);
            profile.highestStage = Math.Max(profile.highestStage, update.highestStage);
            profile.serverVersion++;
            return Task.FromResult(ToSnapshot());
        }

        public Task<BackendSummonResponse> SummonAsync(BackendSession session, BackendSummonRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (session == null || !session.IsValid(clock.UtcNow)) throw new InvalidOperationException("Session is invalid.");
            if (request == null || string.IsNullOrWhiteSpace(request.requestId))
                return Task.FromResult(new BackendSummonResponse { success = false, error = "Request id is required.", gems = profile.gems, summonPity = profile.summonPity });
            var replay = profile.backendSummonReceipts.FirstOrDefault(x => x.requestId == request.requestId);
            if (replay != null) return Task.FromResult(FromReceipt(replay));
            var result = new SummonService(profile, new SystemRandomSource()).SummonOne();
            var response = new BackendSummonResponse
            {
                success = result.Success, error = result.Reason, heroId = result.HeroId,
                rarity = result.Rarity.ToString(), shards = result.Shards, wasPity = result.WasPity,
                gems = profile.gems, summonPity = profile.summonPity
            };
            if (response.success)
            {
                profile.backendSummonReceipts.Add(new BackendSummonReceipt
                {
                    requestId = request.requestId, heroId = response.heroId, rarity = response.rarity,
                    shards = response.shards, wasPity = response.wasPity, gems = response.gems, summonPity = response.summonPity
                });
                if (profile.backendSummonReceipts.Count > 100) profile.backendSummonReceipts.RemoveAt(0);
            }
            return Task.FromResult(response);
        }

        private BackendProfileSnapshot ToSnapshot() => new BackendProfileSnapshot
        {
            playerId = profile.playerId, version = profile.serverVersion, gold = profile.gold, gems = profile.gems,
            highestStage = profile.highestStage, summonPity = profile.summonPity,
            activeFormation = profile.activeFormation.ToArray(),
            heroShards = profile.heroShards.Select(x => new BackendHeroShard { heroId = x.heroId, amount = x.amount }).ToArray()
        };

        private static BackendSummonResponse FromReceipt(BackendSummonReceipt receipt) => new BackendSummonResponse
        {
            success = true, heroId = receipt.heroId, rarity = receipt.rarity, shards = receipt.shards,
            wasPity = receipt.wasPity, gems = receipt.gems, summonPity = receipt.summonPity
        };

        public Task<BackendProgressionResponse> MutateProgressionAsync(BackendSession session,
            BackendProgressionRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (session == null || !session.IsValid(clock.UtcNow)) throw new InvalidOperationException("Session is invalid.");
            if (request == null || string.IsNullOrWhiteSpace(request.requestId))
                return Task.FromResult(MutationFailure("Request id is required."));
            var fingerprint = $"{request.kind}|{request.targetId}";
            var prior = profile.backendMutationReceipts.FirstOrDefault(x => x.requestId == request.requestId);
            if (prior != null)
                return Task.FromResult(prior.fingerprint == fingerprint ? FromReceipt(prior, true) : MutationFailure("Idempotency key collision."));

            EconomyResult result;
            var targetLevel = 0;
            switch (request.kind)
            {
                case BackendProgressionKind.UpgradeBase:
                    result = new MetaProgressionService(profile).UpgradeBase();
                    targetLevel = profile.baseLevel;
                    break;
                case BackendProgressionKind.UpgradeArtifact:
                    if (request.targetId != "war_banner" && request.targetId != "guardian_idol")
                        result = new EconomyResult(false, "Artifact not found.", profile.artifactDust);
                    else
                    {
                        result = new MetaProgressionService(profile).UpgradeArtifact(request.targetId);
                        targetLevel = new MetaProgressionService(profile).GetArtifactLevel(request.targetId);
                    }
                    break;
                case BackendProgressionKind.UpgradeEquipment:
                    result = new EquipmentService(profile).Upgrade(request.targetId);
                    targetLevel = profile.equipment.FirstOrDefault(x => x.instanceId == request.targetId)?.level ?? 0;
                    break;
                case BackendProgressionKind.ClaimAchievement:
                    result = new QuestService(profile).Claim(request.targetId);
                    break;
                case BackendProgressionKind.ClaimMail:
                    var mail = new LiveOpsService(profile, clock).GetInbox(LiveConfigService.Current)
                        .FirstOrDefault(x => x.Id == request.targetId);
                    result = string.IsNullOrEmpty(mail.Id)
                        ? new EconomyResult(false, "Mail not found.", profile.gold)
                        : new LiveOpsService(profile, clock).Claim(mail);
                    break;
                default:
                    result = new EconomyResult(false, "Unknown progression command.", profile.gold);
                    break;
            }
            if (result.Success) profile.serverVersion++;
            var response = new BackendProgressionResponse
            {
                success = result.Success, error = result.Reason, version = profile.serverVersion,
                gold = profile.gold, gems = profile.gems, gearMaterials = profile.gearMaterials,
                artifactDust = profile.artifactDust, baseLevel = profile.baseLevel, targetLevel = targetLevel
            };
            profile.backendMutationReceipts.Add(new BackendMutationReceipt
            {
                requestId = request.requestId, fingerprint = fingerprint, success = response.success, error = response.error,
                version = response.version, gold = response.gold, gems = response.gems,
                gearMaterials = response.gearMaterials, artifactDust = response.artifactDust,
                baseLevel = response.baseLevel, targetLevel = response.targetLevel
            });
            if (profile.backendMutationReceipts.Count > 100) profile.backendMutationReceipts.RemoveAt(0);
            return Task.FromResult(response);
        }

        private BackendProgressionResponse MutationFailure(string error) => new BackendProgressionResponse
        {
            success = false, error = error, version = profile.serverVersion, gold = profile.gold, gems = profile.gems,
            gearMaterials = profile.gearMaterials, artifactDust = profile.artifactDust, baseLevel = profile.baseLevel
        };

        private static BackendProgressionResponse FromReceipt(BackendMutationReceipt receipt, bool replay) => new BackendProgressionResponse
        {
            success = receipt.success, error = receipt.error, wasReplay = replay, version = receipt.version,
            gold = receipt.gold, gems = receipt.gems, gearMaterials = receipt.gearMaterials,
            artifactDust = receipt.artifactDust, baseLevel = receipt.baseLevel, targetLevel = receipt.targetLevel
        };

        public Task<BackendRewardResponse> CompleteBattleAsync(BackendSession session, BackendBattleRequest request,
            CancellationToken cancellationToken)
        {
            ValidateSession(session, cancellationToken);
            var prior = profile.backendRewardReceipts.FirstOrDefault(x => x.requestId == request.requestId);
            if (prior != null)
            {
                var replay = UnityEngine.JsonUtility.FromJson<BackendRewardResponse>(prior.json); replay.wasReplay = true;
                return Task.FromResult(replay);
            }
            if (request == null || string.IsNullOrWhiteSpace(request.ticketId) ||
                !battleTickets.TryGetValue(request.ticketId, out var ticketMode) || ticketMode != request.mode)
                return Task.FromResult(new BackendRewardResponse { success = false, error = "Battle ticket is invalid." });
            if (!ValidTranscript(request))
                return Task.FromResult(new BackendRewardResponse { success = false, error = "Battle transcript is invalid." });
            battleTickets.Remove(request.ticketId);
            var reward = new GameModeService(profile, clock).CompleteVictory(request.mode);
            new RepeatableQuestService(profile, clock).RecordBattleWin();
            profile.serverVersion++;
            var response = new BackendRewardResponse
            {
                success = true, version = profile.serverVersion, gold = profile.gold, gems = profile.gems,
                gearMaterials = profile.gearMaterials, artifactDust = profile.artifactDust,
                highestStage = profile.highestStage, endlessTowerFloor = profile.endlessTowerFloor,
                dailyAttemptsUsed = profile.dailyDungeonAttemptsUsed, totalBattleWins = profile.totalBattleWins,
                rewardGold = reward.Gold, rewardGems = reward.Gems,
                rewardGearMaterials = reward.GearMaterials, rewardArtifactDust = reward.ArtifactDust
            };
            profile.backendRewardReceipts.Add(new BackendRewardReceipt { requestId = request.requestId, json = UnityEngine.JsonUtility.ToJson(response) });
            return Task.FromResult(response);
        }

        private static bool ValidTranscript(BackendBattleRequest request)
        {
            if (request.events == null || request.events.Length == 0 || request.events.Length > 5000 ||
                request.totalSteps <= 0 || request.totalSteps > 36000) return false;
            var transcript = new IdleHeroDefense.Domain.CombatTranscript();
            var previous = 0;
            foreach (var item in request.events)
            {
                if (item.step < previous || item.step > request.totalSteps || item.amount < 0 || item.amount > 100000 ||
                    string.IsNullOrWhiteSpace(item.sourceId) || string.IsNullOrWhiteSpace(item.targetId)) return false;
                previous = item.step;
                transcript.Record(item.step, new IdleHeroDefense.Domain.DamageEvent(item.sourceId, item.targetId, item.amount, item.isUltimate));
            }
            transcript.SetStep(request.totalSteps);
            return string.Equals(transcript.ComputeHash(), request.transcriptHash, StringComparison.OrdinalIgnoreCase);
        }

        public Task<BackendBattleStartResponse> StartBattleAsync(BackendSession session, BackendBattleStartRequest request,
            CancellationToken cancellationToken)
        {
            ValidateSession(session, cancellationToken);
            if (request == null || string.IsNullOrWhiteSpace(request.requestId))
                return Task.FromResult(new BackendBattleStartResponse { success = false, error = "Request id is required." });
            if (battleStarts.TryGetValue(request.requestId, out var replay))
            { replay.wasReplay = true; return Task.FromResult(replay); }
            var modes = new GameModeService(profile, clock);
            if (!modes.TryStart(request.mode, out var reason))
                return Task.FromResult(new BackendBattleStartResponse { success = false, error = reason,
                    energy = new EnergyService(profile, clock).Current, lastEnergyUtcTicks = profile.lastEnergyUtcTicks,
                    serverUtcTicks = clock.UtcNow.UtcDateTime.Ticks });
            var ticket = Guid.NewGuid().ToString("N");
            var response = new BackendBattleStartResponse
            {
                success = true, ticketId = ticket, energy = profile.energy,
                lastEnergyUtcTicks = profile.lastEnergyUtcTicks, serverUtcTicks = clock.UtcNow.UtcDateTime.Ticks
            };
            battleStarts[request.requestId] = response;
            battleTickets[ticket] = request.mode;
            return Task.FromResult(response);
        }

        public Task<BackendIdleResponse> ClaimIdleAsync(BackendSession session, BackendIdleRequest request,
            CancellationToken cancellationToken)
        {
            ValidateSession(session, cancellationToken);
            var prior = profile.backendIdleReceipts.FirstOrDefault(x => x.requestId == request.requestId);
            if (prior != null)
            {
                var replay = UnityEngine.JsonUtility.FromJson<BackendIdleResponse>(prior.json); replay.wasReplay = true;
                return Task.FromResult(replay);
            }
            var reward = new IdleRewardService(profile, clock, TimeSpan.FromHours(LiveConfigService.Current.idleCapHours)).Claim();
            profile.serverVersion++;
            var response = new BackendIdleResponse
            {
                success = true, version = profile.serverVersion, gold = profile.gold, rewardGold = reward.Gold,
                creditedSeconds = (long)reward.CreditedTime.TotalSeconds, lastClaimUtcTicks = profile.lastIdleClaimUtcTicks
            };
            profile.backendIdleReceipts.Add(new BackendIdleReceipt { requestId = request.requestId, json = UnityEngine.JsonUtility.ToJson(response) });
            return Task.FromResult(response);
        }

        public Task<BackendQuestClaimResponse> ClaimRepeatableQuestAsync(BackendSession session,
            BackendQuestClaimRequest request, CancellationToken cancellationToken)
        {
            ValidateSession(session, cancellationToken);
            var prior = profile.backendQuestReceipts.FirstOrDefault(x => x.requestId == request.requestId);
            if (prior != null)
            {
                if (prior.questId != request.questId) return Task.FromResult(new BackendQuestClaimResponse { success = false, error = "Idempotency key collision." });
                var replay = UnityEngine.JsonUtility.FromJson<BackendQuestClaimResponse>(prior.json); replay.wasReplay = true;
                return Task.FromResult(replay);
            }
            var reward = new RepeatableQuestService(profile, clock).Claim(request.questId);
            if (reward.Success) profile.serverVersion++;
            var response = new BackendQuestClaimResponse
            {
                success = reward.Success, error = reward.Error, version = profile.serverVersion,
                gold = profile.gold, gems = profile.gems, rewardGold = reward.Gold, rewardGems = reward.Gems
            };
            profile.backendQuestReceipts.Add(new BackendQuestReceipt
            { requestId = request.requestId, questId = request.questId, json = UnityEngine.JsonUtility.ToJson(response) });
            return Task.FromResult(response);
        }

        public Task<BackendAdClaimResponse> ClaimRewardedAdAsync(BackendSession session, BackendAdClaimRequest request,
            CancellationToken cancellationToken)
        {
            ValidateSession(session, cancellationToken);
            if (request == null || string.IsNullOrWhiteSpace(request.transactionId))
                return Task.FromResult(new BackendAdClaimResponse { success = false, error = "Transaction id is required." });
            if (profile.processedAdTransactionIds.Contains(request.transactionId))
                return Task.FromResult(new BackendAdClaimResponse { success = true, wasReplay = true, version = profile.serverVersion,
                    gems = profile.gems, rewardedAdsWatched = profile.rewardedAdsWatched, rewardedAdUtcDate = profile.rewardedAdUtcDate });
            if (!new RewardedAdService(profile, clock).GrantCompletedView())
                return Task.FromResult(new BackendAdClaimResponse { success = false, error = "Daily rewarded-ad limit reached.", gems = profile.gems });
            profile.processedAdTransactionIds.Add(request.transactionId); profile.serverVersion++;
            return Task.FromResult(new BackendAdClaimResponse { success = true, version = profile.serverVersion,
                gems = profile.gems, rewardedAdsWatched = profile.rewardedAdsWatched, rewardedAdUtcDate = profile.rewardedAdUtcDate });
        }

        private void ValidateSession(BackendSession session, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (session == null || !session.IsValid(clock.UtcNow)) throw new InvalidOperationException("Session is invalid.");
        }
    }
}
