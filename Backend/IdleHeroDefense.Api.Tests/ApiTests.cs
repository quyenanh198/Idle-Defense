using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using IdleHeroDefense.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace IdleHeroDefense.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string storePath = Path.Combine(Path.GetTempPath(), $"idle-hero-defense-{Guid.NewGuid():N}.json");
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("GAME_STORE_PATH", storePath);
        builder.UseEnvironment("Development");
        builder.UseSetting("AllowTestReceipts", "false");
        builder.UseSetting("AdCallbackSecret", "test-secret");
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (File.Exists(storePath)) File.Delete(storePath);
    }
}

public sealed class ApiTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient client;
    public ApiTests(ApiFactory factory)
    {
        client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    }

    [Fact]
    public async Task GuestAuth_ThenEconomy_IsIdempotentAndRejectsCollision()
    {
        var session = await AuthenticateAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var request = new EconomyRequest("tx-1", 0, 0, 100, "test");
        var first = await (await client.PostAsJsonAsync("/v1/economy/transactions", request)).Content.ReadFromJsonAsync<EconomyResponse>();
        var replay = await (await client.PostAsJsonAsync("/v1/economy/transactions", request)).Content.ReadFromJsonAsync<EconomyResponse>();
        var collision = await (await client.PostAsJsonAsync("/v1/economy/transactions", request with { Amount = 999 })).Content.ReadFromJsonAsync<EconomyResponse>();
        Assert.True(first!.Success);
        Assert.True(replay!.WasReplay);
        Assert.Equal(600, replay.Gold);
        Assert.False(collision!.Success);
        Assert.Contains("collision", collision.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProtectedEndpoint_RejectsMissingBearerToken()
    {
        var response = await client.GetAsync("/v1/profile");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReceiptValidation_FailsClosedWithoutPlatformVerifier()
    {
        var session = await AuthenticateAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var receipt = new ReceiptRequest("gems_500", "purchase-1", "untrusted", "test");
        var response = await (await client.PostAsJsonAsync("/v1/iap/validate", receipt)).Content.ReadFromJsonAsync<ValidationResponse>();
        Assert.False(response!.Valid);
    }

    [Fact]
    public async Task Summon_IsServerOwnedAndRequestIdempotent()
    {
        var session = await AuthenticateAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var request = new SummonRequest("summon-1");
        var first = await (await client.PostAsJsonAsync("/v1/summon", request)).Content.ReadFromJsonAsync<SummonResponse>();
        var replay = await (await client.PostAsJsonAsync("/v1/summon", request)).Content.ReadFromJsonAsync<SummonResponse>();
        Assert.True(first!.Success);
        Assert.Equal(first, replay);
        Assert.Equal(0, first.Gems);
        Assert.True(first.Shards is 10 or 30);
    }

    [Fact]
    public async Task ProgressionMutation_AppliesOnceAndRejectsKeyCollision()
    {
        var session = await AuthenticateAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var request = new ProgressionRequest("progress-1", 0, string.Empty);
        var first = await (await client.PostAsJsonAsync("/v1/progression/mutate", request)).Content.ReadFromJsonAsync<ProgressionResponse>();
        var replay = await (await client.PostAsJsonAsync("/v1/progression/mutate", request)).Content.ReadFromJsonAsync<ProgressionResponse>();
        var collision = await (await client.PostAsJsonAsync("/v1/progression/mutate", request with { Kind = 1, TargetId = "war_banner" }))
            .Content.ReadFromJsonAsync<ProgressionResponse>();
        Assert.True(first!.Success);
        Assert.Equal(2, first.BaseLevel);
        Assert.Equal(300, first.Gold);
        Assert.True(replay!.WasReplay);
        Assert.False(collision!.Success);
        Assert.Contains("collision", collision.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BattleReward_IsAuthoritativeAndIdempotent()
    {
        var session = await AuthenticateAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var start = await (await client.PostAsJsonAsync("/v1/battles/start", new BattleStartRequest("start-1", 0)))
            .Content.ReadFromJsonAsync<BattleStartResponse>();
        var transcript = new[] { new TranscriptEvent(10, "ember_knight", "boss", 1740, false) };
        var hash = TranscriptHash(transcript);
        var request = new BattleRewardRequest("battle-1", start!.TicketId, 0, 10, hash, transcript);
        var first = await (await client.PostAsJsonAsync("/v1/rewards/battle", request)).Content.ReadFromJsonAsync<BattleRewardResponse>();
        var replay = await (await client.PostAsJsonAsync("/v1/rewards/battle", request)).Content.ReadFromJsonAsync<BattleRewardResponse>();
        Assert.True(first!.Success);
        Assert.Equal(100, first.RewardGold);
        Assert.Equal(600, first.Gold);
        Assert.Equal(2, first.HighestStage);
        Assert.True(replay!.WasReplay);
        Assert.Equal(600, replay.Gold);
    }

    [Fact]
    public async Task BattleReward_RejectsTamperedTranscript()
    {
        var session = await AuthenticateAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var start = await (await client.PostAsJsonAsync("/v1/battles/start", new BattleStartRequest("tamper-start", 0)))
            .Content.ReadFromJsonAsync<BattleStartResponse>();
        var events = new[] { new TranscriptEvent(10, "ember_knight", "boss", 1740, false) };
        var request = new BattleRewardRequest("tamper-battle", start!.TicketId, 0, 10, new string('0', 64), events);
        var response = await (await client.PostAsJsonAsync("/v1/rewards/battle", request)).Content.ReadFromJsonAsync<BattleRewardResponse>();
        Assert.False(response!.Success);
        Assert.Contains("hash", response.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BattleStart_SpendsEnergyOnceAndRequiresTicket()
    {
        var session = await AuthenticateAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var request = new BattleStartRequest("energy-start", 1);
        var first = await (await client.PostAsJsonAsync("/v1/battles/start", request)).Content.ReadFromJsonAsync<BattleStartResponse>();
        var replay = await (await client.PostAsJsonAsync("/v1/battles/start", request)).Content.ReadFromJsonAsync<BattleStartResponse>();
        Assert.True(first!.Success);
        Assert.Equal(55, first.Energy);
        Assert.True(replay!.WasReplay);
        Assert.Equal(first.TicketId, replay.TicketId);
    }

    [Fact]
    public async Task SignedAdCallback_AllowsExactlyOneRewardClaim()
    {
        var session = await AuthenticateAsync();
        var transactionId = Guid.NewGuid().ToString("N");
        var expires = DateTimeOffset.UtcNow.AddMinutes(5).UtcDateTime.Ticks;
        var canonical = $"{session.PlayerId}|{transactionId}|shop_gems|{expires}";
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("test-secret"), Encoding.UTF8.GetBytes(canonical)));
        var callback = new AdCallbackRequest(session.PlayerId, transactionId, "shop_gems", expires, signature);
        var callbackResponse = await client.PostAsJsonAsync("/v1/ads/callback", callback);
        Assert.Equal(System.Net.HttpStatusCode.Accepted, callbackResponse.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        var claim = new AdClaimRequest(transactionId);
        var first = await (await client.PostAsJsonAsync("/v1/ads/claim", claim)).Content.ReadFromJsonAsync<AdClaimResponse>();
        var replay = await (await client.PostAsJsonAsync("/v1/ads/claim", claim)).Content.ReadFromJsonAsync<AdClaimResponse>();
        Assert.True(first!.Success);
        Assert.Equal(110, first.Gems);
        Assert.True(replay!.WasReplay);
        Assert.Equal(110, replay.Gems);
    }

    private async Task<SessionResponse> AuthenticateAsync()
    {
        var response = await client.PostAsJsonAsync("/v1/auth/guest", new GuestRequest(Guid.NewGuid().ToString("N")));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SessionResponse>())!;
    }

    private static string TranscriptHash(IEnumerable<TranscriptEvent> events)
    {
        var canonical = new StringBuilder();
        foreach (var item in events)
            canonical.Append(item.Step).Append('|').Append(item.SourceId).Append('|').Append(item.TargetId).Append('|')
                .Append(item.Amount).Append('|').Append(item.IsUltimate ? 1 : 0).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }
}
