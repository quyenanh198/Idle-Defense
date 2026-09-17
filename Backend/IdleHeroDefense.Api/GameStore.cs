using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IdleHeroDefense.Api;

public sealed class GameStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string path;
    private StoreState state;

    public GameStore(IHostEnvironment environment)
    {
        var dataDirectory = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDirectory);
        path = Environment.GetEnvironmentVariable("GAME_STORE_PATH") ?? Path.Combine(dataDirectory, "game-store.json");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        state = Load();
    }

    public async Task<SessionResponse> AuthenticateAsync(string installId)
    {
        if (string.IsNullOrWhiteSpace(installId) || installId.Length > 128) throw new ArgumentException("Invalid install id.");
        return await WriteAsync(store =>
        {
            if (!store.InstallToPlayer.TryGetValue(installId, out var playerId))
            {
                playerId = Guid.NewGuid().ToString("N");
                store.InstallToPlayer[installId] = playerId;
                store.Players[playerId] = new PlayerState { PlayerId = playerId, InstallId = installId };
            }
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
            var expires = DateTimeOffset.UtcNow.AddDays(30).UtcDateTime.Ticks;
            foreach (var expired in store.Sessions.Where(x => x.Value.ExpiresUtcTicks <= DateTimeOffset.UtcNow.UtcDateTime.Ticks).Select(x => x.Key).ToList())
                store.Sessions.Remove(expired);
            store.Sessions[Hash(token)] = new SessionState { TokenHash = Hash(token), PlayerId = playerId, ExpiresUtcTicks = expires };
            return new SessionResponse(playerId, token, expires);
        });
    }

    public async Task<PlayerState?> AuthenticateTokenAsync(string? authorization)
    {
        if (string.IsNullOrWhiteSpace(authorization) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
        var hash = Hash(authorization[7..].Trim());
        await gate.WaitAsync();
        try
        {
            if (!state.Sessions.TryGetValue(hash, out var session) || session.ExpiresUtcTicks <= DateTimeOffset.UtcNow.UtcDateTime.Ticks) return null;
            return state.Players.TryGetValue(session.PlayerId, out var player) ? Clone(player) : null;
        }
        finally { gate.Release(); }
    }

    public Task<EconomyResponse> EconomyAsync(string playerId, EconomyRequest request) => WriteAsync(store =>
    {
        var player = store.Players[playerId];
        if (string.IsNullOrWhiteSpace(request.RequestId)) return new EconomyResponse(false, "Invalid transaction.", player.Gold, player.Gems);
        var fingerprint = $"{request.Operation}|{request.Currency}|{request.Amount}|{request.Reason}";
        if (player.EconomyReceipts.TryGetValue(request.RequestId, out var receipt))
            return receipt.Fingerprint == fingerprint ? receipt.Response with { WasReplay = true }
                : new EconomyResponse(false, "Idempotency key collision.", player.Gold, player.Gems);
        if (request.Amount <= 0 || request.Operation is < 0 or > 1 || request.Currency is < 0 or > 1)
            return new EconomyResponse(false, "Invalid transaction.", player.Gold, player.Gems);
        var balance = request.Currency == 0 ? player.Gold : player.Gems;
        if (request.Operation == 1 && balance < request.Amount)
        {
            var failed = new EconomyResponse(false, "Insufficient currency.", player.Gold, player.Gems);
            player.EconomyReceipts[request.RequestId] = new EconomyReceipt { Fingerprint = fingerprint, Response = failed };
            return failed;
        }
        var delta = request.Operation == 0 ? request.Amount : -request.Amount;
        if (request.Currency == 0) player.Gold = checked(player.Gold + delta); else player.Gems = checked(player.Gems + delta);
        if (request.Operation == 1 && request.Currency == 0 && request.Reason?.StartsWith("hero_level:", StringComparison.Ordinal) == true)
        {
            var parts = request.Reason.Split(':');
            if (parts.Length != 3 || !int.TryParse(parts[2], out var requestedLevel))
                return new EconomyResponse(false, "Invalid hero upgrade command.", player.Gold -= delta, player.Gems);
            var currentLevel = player.HeroLevels.GetValueOrDefault(parts[1], 1);
            var expectedCost = 50 + (currentLevel - 1) * 25;
            if (requestedLevel != currentLevel + 1 || request.Amount != expectedCost)
                return new EconomyResponse(false, "Hero upgrade validation failed.", player.Gold -= delta, player.Gems);
            player.HeroLevels[parts[1]] = requestedLevel;
            RefreshQuestPeriods(player);
            player.DailyHeroUpgrades++;
        }
        player.Version++;
        var response = new EconomyResponse(true, string.Empty, player.Gold, player.Gems);
        player.EconomyReceipts[request.RequestId] = new EconomyReceipt { Fingerprint = fingerprint, Response = response };
        Trim(player.EconomyReceipts, 500);
        return response;
    });

    public Task<SummonResponse> SummonAsync(string playerId, SummonRequest request) => WriteAsync(store =>
    {
        var player = store.Players[playerId];
        if (string.IsNullOrWhiteSpace(request.RequestId))
            return new SummonResponse(false, "Invalid request.", string.Empty, string.Empty, 0, false, player.Gems, player.SummonPity);
        if (player.SummonReceipts.TryGetValue(request.RequestId, out var replay)) return replay;
        if (player.Gems < 100)
            return new SummonResponse(false, "Invalid request or insufficient gems.", string.Empty, string.Empty, 0, false, player.Gems, player.SummonPity);
        player.Gems -= 100;
        var pity = player.SummonPity + 1 >= 10;
        var epic = pity || RandomNumberGenerator.GetInt32(100) < 10;
        var pool = epic ? new[] { "shade_mage", "sun_priest", "storm_hunter" } : new[] { "forest_archer", "ember_knight", "iron_guard" };
        var hero = pool[RandomNumberGenerator.GetInt32(pool.Length)];
        var shards = epic ? 30 : 10;
        player.HeroShards[hero] = player.HeroShards.GetValueOrDefault(hero) + shards;
        player.SummonPity = epic ? 0 : player.SummonPity + 1;
        RefreshQuestPeriods(player);
        player.DailySummons++;
        player.Version++;
        var response = new SummonResponse(true, string.Empty, hero, epic ? "Epic" : "Rare", shards, pity, player.Gems, player.SummonPity);
        player.SummonReceipts[request.RequestId] = response;
        Trim(player.SummonReceipts, 500);
        return response;
    });

    public Task<PlayerState?> GetPlayerAsync(string playerId) => ReadAsync(store => store.Players.TryGetValue(playerId, out var player) ? Clone(player) : null);

    public Task<PlayerState?> UpdateProfileAsync(string playerId, ProfileUpdateRequest request) => WriteAsync(store =>
    {
        var player = store.Players[playerId];
        if (player.Version != request.ExpectedVersion || request.HighestStage != player.HighestStage ||
            request.ActiveFormation.Count is < 1 or > 5) return null;
        if (request.ActiveFormation.Distinct().Count() != request.ActiveFormation.Count) return null;
        player.ActiveFormation = request.ActiveFormation.ToList();
        player.Version++;
        return Clone(player);
    });

    public Task RecordAnalyticsAsync(AnalyticsBatch batch) => WriteAsync<object?>(store =>
    {
        foreach (var item in batch.Events.Take(100)) if (!string.IsNullOrWhiteSpace(item.Id)) store.AnalyticsEventIds.Add(item.Id);
        while (store.AnalyticsEventIds.Count > 10_000) store.AnalyticsEventIds.Remove(store.AnalyticsEventIds.First());
        return null;
    });

    public Task<bool> MarkPurchaseAsync(string playerId, string transactionId, string productId) => WriteAsync(store =>
    {
        var player = store.Players[playerId];
        if (!player.PurchaseTransactions.Add(transactionId)) return true;
        var gems = productId switch { "gems_500" => 500, "gems_1200" => 1200, "founder_badge" => 300, _ => 0 };
        if (gems <= 0) { player.PurchaseTransactions.Remove(transactionId); return false; }
        player.Gems = checked(player.Gems + gems);
        if (productId == "founder_badge") player.Entitlements.Add("founder_badge");
        player.Version++;
        return true;
    });

    public Task<ProgressionResponse> MutateProgressionAsync(string playerId, ProgressionRequest request) => WriteAsync(store =>
    {
        var player = store.Players[playerId];
        if (string.IsNullOrWhiteSpace(request.RequestId)) return ProgressionFailure(player, "Request id is required.");
        var fingerprint = $"{request.Kind}|{request.TargetId}";
        if (player.ProgressionReceipts.TryGetValue(request.RequestId, out var prior))
            return prior.Fingerprint == fingerprint ? prior.Response with { WasReplay = true }
                : ProgressionFailure(player, "Idempotency key collision.");
        var success = false;
        var error = string.Empty;
        var targetLevel = 0;
        switch (request.Kind)
        {
            case 0:
                var baseCost = player.BaseLevel * 200;
                if (player.Gold < baseCost) error = "Insufficient gold.";
                else { player.Gold -= baseCost; player.BaseLevel++; targetLevel = player.BaseLevel; success = true; }
                break;
            case 1:
                if (!player.ArtifactLevels.TryGetValue(request.TargetId, out var artifactLevel)) error = "Artifact not found.";
                else if (player.ArtifactDust < artifactLevel * 50) error = "Insufficient artifact dust.";
                else { player.ArtifactDust -= artifactLevel * 50; targetLevel = ++player.ArtifactLevels[request.TargetId]; success = true; }
                break;
            case 2:
                if (!player.EquipmentLevels.TryGetValue(request.TargetId, out var equipmentLevel)) error = "Equipment not found.";
                else if (player.GearMaterials < equipmentLevel * 25) error = "Insufficient gear materials.";
                else { player.GearMaterials -= equipmentLevel * 25; targetLevel = ++player.EquipmentLevels[request.TargetId]; success = true; }
                break;
            case 3:
                if (player.ClaimedAchievements.Contains(request.TargetId)) error = "Achievement already claimed.";
                else
                {
                    var eligible = request.TargetId switch
                    {
                        "first_win" => player.TotalWins >= 1,
                        "stage_five" => player.HighestStage >= 5,
                        "hero_level_three" => player.HeroLevels.Values.DefaultIfEmpty(1).Max() >= 3,
                        _ => false
                    };
                    if (!eligible) error = "Achievement is not complete.";
                    else
                    {
                        var reward = request.TargetId == "stage_five" ? 250 : request.TargetId == "hero_level_three" ? 150 : 100;
                        player.Gold += reward; player.ClaimedAchievements.Add(request.TargetId); success = true;
                    }
                }
                break;
            case 4:
                if (request.TargetId != "welcome_v1") error = "Mail not found or expired.";
                else if (!player.ClaimedMail.Add(request.TargetId)) error = "Mail already claimed.";
                else { player.Gold += 250; player.Gems += 25; success = true; }
                break;
            default: error = "Unknown progression command."; break;
        }
        if (success) player.Version++;
        var response = new ProgressionResponse(success, error, false, player.Version, player.Gold, player.Gems,
            player.GearMaterials, player.ArtifactDust, player.BaseLevel, targetLevel);
        player.ProgressionReceipts[request.RequestId] = new ProgressionReceipt(fingerprint, response);
        Trim(player.ProgressionReceipts, 500);
        return response;
    });

    public Task<BattleRewardResponse> CompleteBattleAsync(string playerId, BattleRewardRequest request) => WriteAsync(store =>
    {
        var player = store.Players[playerId];
        if (string.IsNullOrWhiteSpace(request.RequestId)) return BattleFailure(player, "Request id is required.");
        if (player.BattleReceipts.TryGetValue(request.RequestId, out var prior))
            return prior.Mode == request.Mode ? prior.Response with { WasReplay = true } : BattleFailure(player, "Idempotency key collision.");
        if (string.IsNullOrWhiteSpace(request.TicketId) || !store.BattleTickets.TryGetValue(request.TicketId, out var ticket) ||
            ticket.PlayerId != playerId || ticket.Mode != request.Mode || ticket.Consumed ||
            ticket.ExpiresUtcTicks <= DateTimeOffset.UtcNow.UtcDateTime.Ticks)
            return BattleFailure(player, "Battle ticket is invalid or expired.");
        if (!BattleTranscriptVerifier.Verify(request, ticket, out var transcriptError))
            return BattleFailure(player, transcriptError);
        store.BattleTickets.Remove(request.TicketId);
        RefreshQuestPeriods(player);
        var rewardGold = 0; var rewardGems = 0; var rewardGear = 0; var rewardDust = 0;
        if (request.Mode == 0) { rewardGold = 100; player.HighestStage++; }
        else if (request.Mode == 1)
        {
            RefreshDailyAttempts(player);
            if (player.DailyAttemptsUsed >= 3) return BattleFailure(player, "No daily attempts remaining.");
            player.DailyAttemptsUsed++; rewardGold = 300; rewardGear = 50;
        }
        else if (request.Mode == 2)
        {
            rewardGold = 150 + player.EndlessTowerFloor * 25;
            rewardGems = player.EndlessTowerFloor % 5 == 0 ? 20 : 0;
            rewardDust = 25; player.EndlessTowerFloor++;
        }
        else return BattleFailure(player, "Unknown game mode.");
        player.Gold += rewardGold; player.Gems += rewardGems;
        player.GearMaterials += rewardGear; player.ArtifactDust += rewardDust;
        player.TotalWins++; player.DailyWins++; player.WeeklyWins++; player.Version++;
        var response = new BattleRewardResponse(true, string.Empty, false, player.Version, player.Gold, player.Gems,
            player.GearMaterials, player.ArtifactDust, player.HighestStage, player.EndlessTowerFloor,
            player.DailyAttemptsUsed, player.TotalWins, rewardGold, rewardGems, rewardGear, rewardDust);
        player.BattleReceipts[request.RequestId] = new BattleReceipt(request.Mode, response);
        Trim(player.BattleReceipts, 500);
        return response;
    });

    public Task<BattleStartResponse> StartBattleAsync(string playerId, BattleStartRequest request) => WriteAsync(store =>
    {
        var player = store.Players[playerId];
        RegenerateEnergy(player);
        var nowTicks = DateTimeOffset.UtcNow.UtcDateTime.Ticks;
        if (string.IsNullOrWhiteSpace(request.RequestId))
            return new BattleStartResponse(false, "Request id is required.", false, string.Empty, player.Energy, player.LastEnergyUtcTicks, nowTicks);
        if (player.BattleStartReceipts.TryGetValue(request.RequestId, out var prior))
            return prior.Mode == request.Mode ? prior.Response with { WasReplay = true }
                : new BattleStartResponse(false, "Idempotency key collision.", false, string.Empty, player.Energy, player.LastEnergyUtcTicks, nowTicks);
        RefreshDailyAttempts(player);
        var cost = request.Mode == 1 ? 5 : request.Mode == 2 ? 3 : request.Mode == 0 ? 0 : -1;
        var error = cost < 0 ? "Unknown game mode." : request.Mode == 1 && player.DailyAttemptsUsed >= 3
            ? "No daily attempts remaining." : player.Energy < cost ? "Not enough energy." : string.Empty;
        if (!string.IsNullOrEmpty(error))
            return new BattleStartResponse(false, error, false, string.Empty, player.Energy, player.LastEnergyUtcTicks, nowTicks);
        player.Energy -= cost;
        var ticketId = Guid.NewGuid().ToString("N");
        var response = new BattleStartResponse(true, string.Empty, false, ticketId, player.Energy, player.LastEnergyUtcTicks, nowTicks);
        player.BattleStartReceipts[request.RequestId] = new BattleStartReceipt(request.Mode, response);
        var scale = request.Mode == 0 ? 1f + (player.HighestStage - 1) * 0.08f
            : request.Mode == 1 ? 1.35f : 1f + (player.EndlessTowerFloor - 1) * 0.12f;
        var requiredDamage = (int)(160 * scale) * 2 + (int)(260 * scale) * 2 + (int)(900 * scale);
        store.BattleTickets[ticketId] = new BattleTicket(playerId, request.Mode,
            DateTimeOffset.UtcNow.AddMinutes(30).UtcDateTime.Ticks, false, player.ActiveFormation.ToArray(), requiredDamage);
        Trim(player.BattleStartReceipts, 500);
        return response;
    });

    public Task<IdleRewardResponse> ClaimIdleAsync(string playerId, IdleRewardRequest request) => WriteAsync(store =>
    {
        var player = store.Players[playerId];
        if (string.IsNullOrWhiteSpace(request.RequestId)) return IdleFailure(player, "Request id is required.");
        if (player.IdleReceipts.TryGetValue(request.RequestId, out var prior)) return prior with { WasReplay = true };
        var now = DateTimeOffset.UtcNow;
        var last = new DateTimeOffset(player.LastIdleClaimUtcTicks, TimeSpan.Zero);
        var seconds = (long)Math.Clamp((now - last).TotalSeconds, 0, TimeSpan.FromHours(8).TotalSeconds);
        var minutes = seconds / 60;
        var reward = checked((int)(minutes * 2 * Math.Max(1, player.HighestStage)));
        player.Gold += reward; player.LastIdleClaimUtcTicks = now.UtcDateTime.Ticks; player.Version++;
        var response = new IdleRewardResponse(true, string.Empty, false, player.Version, player.Gold, reward,
            minutes * 60, player.LastIdleClaimUtcTicks);
        player.IdleReceipts[request.RequestId] = response;
        Trim(player.IdleReceipts, 500);
        return response;
    });

    public Task<QuestClaimResponse> ClaimRepeatableQuestAsync(string playerId, QuestClaimRequest request) => WriteAsync(store =>
    {
        var player = store.Players[playerId];
        if (string.IsNullOrWhiteSpace(request.RequestId)) return QuestFailure(player, "Request id is required.");
        if (player.QuestReceipts.TryGetValue(request.RequestId, out var prior))
            return prior.QuestId == request.QuestId ? prior.Response with { WasReplay = true } : QuestFailure(player, "Idempotency key collision.");
        RefreshQuestPeriods(player);
        var daily = request.QuestId.StartsWith("daily_", StringComparison.Ordinal);
        var claims = daily ? player.DailyQuestClaims : player.WeeklyQuestClaims;
        var eligible = request.QuestId switch
        {
            "daily_win" => player.DailyWins >= 1,
            "daily_upgrade" => player.DailyHeroUpgrades >= 1,
            "daily_summon" => player.DailySummons >= 1,
            "weekly_wins" => player.WeeklyWins >= 10,
            _ => false
        };
        var success = eligible && claims.Add(request.QuestId);
        var error = !eligible ? "Quest is not complete." : !success ? "Quest already claimed." : string.Empty;
        var rewardGold = success ? request.QuestId switch { "daily_win" => 75, "daily_upgrade" => 75, "weekly_wins" => 500, _ => 0 } : 0;
        var rewardGems = success ? request.QuestId switch { "daily_summon" => 10, "weekly_wins" => 50, _ => 0 } : 0;
        if (success) { player.Gold += rewardGold; player.Gems += rewardGems; player.Version++; }
        var response = new QuestClaimResponse(success, error, false, player.Version, player.Gold, player.Gems, rewardGold, rewardGems);
        player.QuestReceipts[request.RequestId] = new QuestReceipt(request.QuestId, response);
        Trim(player.QuestReceipts, 500);
        return response;
    });

    public Task<bool> RegisterVerifiedAdAsync(AdCallbackRequest request) => WriteAsync(store =>
    {
        if (!store.Players.ContainsKey(request.PlayerId) || string.IsNullOrWhiteSpace(request.TransactionId)) return false;
        if (store.VerifiedAds.TryGetValue(request.TransactionId, out var existing))
            return existing.PlayerId == request.PlayerId && existing.Placement == request.Placement;
        store.VerifiedAds[request.TransactionId] = new VerifiedAd(request.PlayerId, request.Placement, request.ExpiresUtcTicks);
        return true;
    });

    public Task<AdClaimResponse> ClaimAdAsync(string playerId, AdClaimRequest request) => WriteAsync(store =>
    {
        var player = store.Players[playerId];
        RefreshAdDay(player);
        if (player.ProcessedAdTransactions.Contains(request.TransactionId))
            return new AdClaimResponse(true, string.Empty, true, player.Version, player.Gems,
                player.RewardedAdsWatched, player.RewardedAdUtcDate);
        if (!store.VerifiedAds.TryGetValue(request.TransactionId, out var verified) || verified.PlayerId != playerId ||
            verified.Placement != "shop_gems" || verified.ExpiresUtcTicks < DateTimeOffset.UtcNow.UtcDateTime.Ticks)
            return new AdClaimResponse(false, "Ad completion is not server-verified.", false, player.Version, player.Gems,
                player.RewardedAdsWatched, player.RewardedAdUtcDate);
        if (player.RewardedAdsWatched >= 5)
            return new AdClaimResponse(false, "Daily rewarded-ad limit reached.", false, player.Version, player.Gems,
                player.RewardedAdsWatched, player.RewardedAdUtcDate);
        player.ProcessedAdTransactions.Add(request.TransactionId); store.VerifiedAds.Remove(request.TransactionId);
        player.RewardedAdsWatched++; player.Gems += 10; player.Version++;
        return new AdClaimResponse(true, string.Empty, false, player.Version, player.Gems,
            player.RewardedAdsWatched, player.RewardedAdUtcDate);
    });

    private static ProgressionResponse ProgressionFailure(PlayerState player, string error) => new(false, error, false,
        player.Version, player.Gold, player.Gems, player.GearMaterials, player.ArtifactDust, player.BaseLevel, 0);
    private static BattleRewardResponse BattleFailure(PlayerState p, string error) => new(false, error, false, p.Version,
        p.Gold, p.Gems, p.GearMaterials, p.ArtifactDust, p.HighestStage, p.EndlessTowerFloor, p.DailyAttemptsUsed,
        p.TotalWins, 0, 0, 0, 0);
    private static IdleRewardResponse IdleFailure(PlayerState p, string error) => new(false, error, false, p.Version,
        p.Gold, 0, 0, p.LastIdleClaimUtcTicks);
    private static QuestClaimResponse QuestFailure(PlayerState p, string error) => new(false, error, false, p.Version,
        p.Gold, p.Gems, 0, 0);

    private static void RefreshDailyAttempts(PlayerState player)
    {
        var date = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
        if (player.DailyAttemptDate == date) return;
        player.DailyAttemptDate = date; player.DailyAttemptsUsed = 0;
    }

    private static void RefreshQuestPeriods(PlayerState player)
    {
        var now = DateTimeOffset.UtcNow;
        var day = now.ToString("yyyy-MM-dd");
        if (player.DailyQuestDate != day)
        {
            player.DailyQuestDate = day; player.DailyWins = 0; player.DailyHeroUpgrades = 0; player.DailySummons = 0;
            player.DailyQuestClaims.Clear();
        }
        var monday = now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7)).ToString("yyyy-MM-dd");
        if (player.WeeklyQuestMonday != monday)
        { player.WeeklyQuestMonday = monday; player.WeeklyWins = 0; player.WeeklyQuestClaims.Clear(); }
    }

    private static void RegenerateEnergy(PlayerState player)
    {
        var now = DateTimeOffset.UtcNow;
        if (player.LastEnergyUtcTicks <= 0) player.LastEnergyUtcTicks = now.UtcDateTime.Ticks;
        if (player.Energy >= 60) { player.Energy = 60; player.LastEnergyUtcTicks = now.UtcDateTime.Ticks; return; }
        var last = new DateTimeOffset(player.LastEnergyUtcTicks, TimeSpan.Zero);
        var recovered = (int)Math.Max(0, (now - last).Ticks / TimeSpan.FromMinutes(5).Ticks);
        if (recovered <= 0) return;
        player.Energy = Math.Min(60, player.Energy + recovered);
        player.LastEnergyUtcTicks = player.Energy >= 60 ? now.UtcDateTime.Ticks
            : last.AddTicks(recovered * TimeSpan.FromMinutes(5).Ticks).UtcDateTime.Ticks;
    }

    private static void RefreshAdDay(PlayerState player)
    {
        var today = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
        if (player.RewardedAdUtcDate == today) return;
        player.RewardedAdUtcDate = today; player.RewardedAdsWatched = 0;
    }

    private async Task<T> ReadAsync<T>(Func<StoreState, T> action)
    { await gate.WaitAsync(); try { return action(state); } finally { gate.Release(); } }

    private async Task<T> WriteAsync<T>(Func<StoreState, T> action)
    {
        await gate.WaitAsync();
        try { var result = action(state); await SaveAsync(); return result; }
        finally { gate.Release(); }
    }

    private StoreState Load()
    {
        if (!File.Exists(path)) return new StoreState();
        try { return JsonSerializer.Deserialize<StoreState>(File.ReadAllText(path), JsonOptions) ?? new StoreState(); }
        catch { return new StoreState(); }
    }

    private async Task SaveAsync()
    {
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(state, JsonOptions));
        File.Move(temporary, path, true);
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static PlayerState Clone(PlayerState player) => JsonSerializer.Deserialize<PlayerState>(JsonSerializer.Serialize(player, JsonOptions), JsonOptions)!;
    private static void Trim<T>(Dictionary<string, T> values, int maximum)
    { while (values.Count > maximum) values.Remove(values.Keys.First()); }
}
