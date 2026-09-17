using System.Security.Cryptography;
using System.Text;

namespace IdleHeroDefense.Api;

public static class BattleTranscriptVerifier
{
    public static bool Verify(BattleRewardRequest request, BattleTicket ticket, out string error)
    {
        error = string.Empty;
        if (request.Events == null || request.Events.Count == 0 || request.Events.Count > 5000 ||
            request.TotalSteps <= 0 || request.TotalSteps > 36000)
        { error = "Battle transcript size is invalid."; return false; }

        var heroes = (ticket.AllowedHeroes ?? Array.Empty<string>()).ToHashSet(StringComparer.Ordinal);
        long heroDamage = 0;
        var previousStep = 0;
        var canonical = new StringBuilder();
        foreach (var item in request.Events)
        {
            if (item.Step < previousStep || item.Step > request.TotalSteps || item.Step < 0 ||
                item.Amount < 0 || item.Amount > 100000 || string.IsNullOrWhiteSpace(item.SourceId) ||
                string.IsNullOrWhiteSpace(item.TargetId) || item.SourceId.Length > 64 || item.TargetId.Length > 64)
            { error = "Battle transcript contains an invalid event."; return false; }
            previousStep = item.Step;
            if (heroes.Contains(item.SourceId)) heroDamage += item.Amount;
            canonical.Append(item.Step).Append('|').Append(item.SourceId).Append('|').Append(item.TargetId).Append('|')
                .Append(item.Amount).Append('|').Append(item.IsUltimate ? 1 : 0).Append('\n');
        }

        var actual = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
        if (request.TranscriptHash?.Length != 64 ||
            !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(actual), Encoding.ASCII.GetBytes(request.TranscriptHash.ToUpperInvariant())))
        { error = "Battle transcript hash mismatch."; return false; }
        if (heroDamage != ticket.RequiredEnemyDamage)
        { error = "Battle transcript does not prove victory."; return false; }
        return true;
    }
}
