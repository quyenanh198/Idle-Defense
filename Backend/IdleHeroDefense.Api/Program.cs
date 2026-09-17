using IdleHeroDefense.Api;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<GameStore>();
builder.Services.AddSingleton<IReceiptVerifier, SafeReceiptVerifier>();
builder.Services.AddSingleton<AdCallbackVerifier>();
builder.Services.AddHealthChecks();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("mutation", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
app.UseHttpsRedirection();
app.UseRateLimiter();
app.MapHealthChecks("/health");

app.MapPost("/v1/auth/guest", async (GuestRequest request, GameStore store) =>
{
    try { return Results.Ok(await store.AuthenticateAsync(request.InstallId)); }
    catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
}).RequireRateLimiting("auth");

app.MapPost("/v1/economy/transactions", async (HttpContext context, EconomyRequest request, GameStore store) =>
{
    var player = await store.AuthenticateTokenAsync(context.Request.Headers.Authorization);
    return player == null ? Results.Unauthorized() : Results.Ok(await store.EconomyAsync(player.PlayerId, request));
});

app.MapPost("/v1/summon", async (HttpContext context, SummonRequest request, GameStore store) =>
{
    var player = await store.AuthenticateTokenAsync(context.Request.Headers.Authorization);
    return player == null ? Results.Unauthorized() : Results.Ok(await store.SummonAsync(player.PlayerId, request));
});

app.MapPost("/v1/progression/mutate", async (HttpContext context, ProgressionRequest request, GameStore store) =>
{
    var player = await store.AuthenticateTokenAsync(context.Request.Headers.Authorization);
    return player == null ? Results.Unauthorized() : Results.Ok(await store.MutateProgressionAsync(player.PlayerId, request));
});

app.MapPost("/v1/rewards/battle", async (HttpContext context, BattleRewardRequest request, GameStore store) =>
{
    var player = await store.AuthenticateTokenAsync(context.Request.Headers.Authorization);
    return player == null ? Results.Unauthorized() : Results.Ok(await store.CompleteBattleAsync(player.PlayerId, request));
}).RequireRateLimiting("mutation");

app.MapPost("/v1/battles/start", async (HttpContext context, BattleStartRequest request, GameStore store) =>
{
    var player = await store.AuthenticateTokenAsync(context.Request.Headers.Authorization);
    return player == null ? Results.Unauthorized() : Results.Ok(await store.StartBattleAsync(player.PlayerId, request));
}).RequireRateLimiting("mutation");

app.MapPost("/v1/rewards/idle", async (HttpContext context, IdleRewardRequest request, GameStore store) =>
{
    var player = await store.AuthenticateTokenAsync(context.Request.Headers.Authorization);
    return player == null ? Results.Unauthorized() : Results.Ok(await store.ClaimIdleAsync(player.PlayerId, request));
}).RequireRateLimiting("mutation");

app.MapPost("/v1/rewards/quest", async (HttpContext context, QuestClaimRequest request, GameStore store) =>
{
    var player = await store.AuthenticateTokenAsync(context.Request.Headers.Authorization);
    return player == null ? Results.Unauthorized() : Results.Ok(await store.ClaimRepeatableQuestAsync(player.PlayerId, request));
}).RequireRateLimiting("mutation");

app.MapPost("/v1/ads/callback", async (AdCallbackRequest request, GameStore store, AdCallbackVerifier verifier) =>
{
    if (!verifier.Verify(request)) return Results.Unauthorized();
    return await store.RegisterVerifiedAdAsync(request) ? Results.Accepted() : Results.BadRequest();
});

app.MapPost("/v1/ads/claim", async (HttpContext context, AdClaimRequest request, GameStore store) =>
{
    var player = await store.AuthenticateTokenAsync(context.Request.Headers.Authorization);
    return player == null ? Results.Unauthorized() : Results.Ok(await store.ClaimAdAsync(player.PlayerId, request));
}).RequireRateLimiting("mutation");

app.MapGet("/v1/profile", async (HttpContext context, GameStore store) =>
{
    var player = await store.AuthenticateTokenAsync(context.Request.Headers.Authorization);
    if (player == null) return Results.Unauthorized();
    var current = await store.GetPlayerAsync(player.PlayerId);
    return current == null ? Results.NotFound() : Results.Ok(ToProfile(current));
});

app.MapPut("/v1/profile", async (HttpContext context, ProfileUpdateRequest request, GameStore store) =>
{
    var player = await store.AuthenticateTokenAsync(context.Request.Headers.Authorization);
    if (player == null) return Results.Unauthorized();
    var updated = await store.UpdateProfileAsync(player.PlayerId, request);
    return updated == null ? Results.Conflict(new { error = "Profile version or progression validation failed." }) : Results.Ok(ToProfile(updated));
});

app.MapPost("/v1/iap/validate", async (HttpContext context, ReceiptRequest request, GameStore store, IReceiptVerifier verifier, CancellationToken token) =>
{
    var player = await store.AuthenticateTokenAsync(context.Request.Headers.Authorization);
    if (player == null) return Results.Unauthorized();
    if (!await verifier.VerifyAsync(request, token)) return Results.Ok(new ValidationResponse(false, "Receipt rejected.", request.TransactionId));
    var delivered = await store.MarkPurchaseAsync(player.PlayerId, request.TransactionId, request.ProductId);
    return Results.Ok(new ValidationResponse(delivered, delivered ? string.Empty : "Unknown product.", request.TransactionId));
});

app.MapPost("/v1/analytics/batch", async (AnalyticsBatch batch, GameStore store) =>
{
    await store.RecordAnalyticsAsync(batch);
    return Results.Accepted();
});

app.Run();

static ProfileResponse ToProfile(PlayerState player) => new(player.PlayerId, player.Version, player.Gold, player.Gems,
    player.HighestStage, player.SummonPity, player.GearMaterials, player.ArtifactDust, player.BaseLevel,
    player.EndlessTowerFloor, player.DailyAttemptsUsed, player.DailyAttemptDate, player.TotalWins, player.Energy, player.LastEnergyUtcTicks, player.LastIdleClaimUtcTicks,
    player.DailyQuestDate, player.WeeklyQuestMonday, player.DailyWins, player.DailyHeroUpgrades,
    player.DailySummons, player.WeeklyWins, player.DailyQuestClaims.ToArray(), player.WeeklyQuestClaims.ToArray(), player.ActiveFormation,
    player.HeroShards.Select(x => new HeroShardResponse(x.Key, x.Value)).ToArray(),
    player.HeroLevels.Select(x => new LevelResponse(x.Key, x.Value)).ToArray(),
    player.EquipmentLevels.Select(x => new LevelResponse(x.Key, x.Value)).ToArray(),
    player.ArtifactLevels.Select(x => new LevelResponse(x.Key, x.Value)).ToArray(),
    player.ClaimedAchievements.ToArray(), player.ClaimedMail.ToArray(), player.Entitlements.ToArray(),
    player.RewardedAdsWatched, player.RewardedAdUtcDate);

public partial class Program { }
