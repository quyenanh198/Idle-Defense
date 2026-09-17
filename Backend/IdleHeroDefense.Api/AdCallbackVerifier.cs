using System.Security.Cryptography;
using System.Text;

namespace IdleHeroDefense.Api;

public sealed class AdCallbackVerifier(IConfiguration configuration)
{
    public bool Verify(AdCallbackRequest request)
    {
        var secret = configuration["AdCallbackSecret"];
        var now = DateTimeOffset.UtcNow;
        if (string.IsNullOrWhiteSpace(secret) || request.ExpiresUtcTicks < now.UtcDateTime.Ticks ||
            request.ExpiresUtcTicks > now.AddMinutes(10).UtcDateTime.Ticks) return false;
        var canonical = $"{request.PlayerId}|{request.TransactionId}|{request.Placement}|{request.ExpiresUtcTicks}";
        var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(canonical)));
        var provided = request.Signature?.ToUpperInvariant() ?? string.Empty;
        return provided.Length == expected.Length && CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(provided), Encoding.ASCII.GetBytes(expected));
    }
}
