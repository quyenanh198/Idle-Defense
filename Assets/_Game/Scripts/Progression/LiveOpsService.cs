using System;
using System.Collections.Generic;
using IdleHeroDefense.Infrastructure;

namespace IdleHeroDefense.Progression
{
    public readonly struct InboxMessage
    {
        public readonly string Id;
        public readonly string Title;
        public readonly int Gold;
        public readonly int Gems;
        public readonly DateTimeOffset ExpiresUtc;

        public InboxMessage(string id, string title, int gold, int gems, DateTimeOffset expiresUtc)
        { Id = id; Title = title; Gold = gold; Gems = gems; ExpiresUtc = expiresUtc; }
    }

    public sealed class LiveOpsService
    {
        private readonly PlayerProfile profile;
        private readonly IClock clock;
        public LiveOpsService(PlayerProfile profile, IClock clock)
        { this.profile = profile ?? throw new ArgumentNullException(nameof(profile)); this.clock = clock ?? throw new ArgumentNullException(nameof(clock)); }

        public IReadOnlyList<InboxMessage> GetInbox(LiveConfig config)
        {
            var result = new List<InboxMessage>
            {
                new InboxMessage("welcome_v1", "Welcome, Defender!", 250, 25, DateTimeOffset.MaxValue)
            };
            foreach (var liveEvent in config.events)
            {
                if (!TryParseWindow(liveEvent, out var starts, out var ends)) continue;
                if (clock.UtcNow < starts || clock.UtcNow >= ends) continue;
                result.Add(new InboxMessage($"event_{liveEvent.id}", liveEvent.title, liveEvent.mailGold, liveEvent.mailGems, ends));
            }
            return result;
        }

        public EconomyResult Claim(InboxMessage message)
        {
            if (profile.claimedMailIds.Contains(message.Id)) return new EconomyResult(false, "Mail already claimed.", profile.gold);
            if (clock.UtcNow >= message.ExpiresUtc) return new EconomyResult(false, "Mail has expired.", profile.gold);
            var economy = new EconomyService(profile);
            if (message.Gold > 0) economy.Grant(CurrencyType.Gold, message.Gold);
            if (message.Gems > 0) economy.Grant(CurrencyType.Gems, message.Gems);
            profile.claimedMailIds.Add(message.Id);
            return new EconomyResult(true, string.Empty, profile.gold);
        }

        private static bool TryParseWindow(LiveEventConfig config, out DateTimeOffset starts, out DateTimeOffset ends) =>
            DateTimeOffset.TryParse(config.startsUtc, out starts) && DateTimeOffset.TryParse(config.endsUtc, out ends) && ends > starts;
    }
}
