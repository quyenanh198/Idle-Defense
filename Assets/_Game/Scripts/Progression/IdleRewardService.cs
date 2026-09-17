using System;

namespace IdleHeroDefense.Progression
{
    public readonly struct IdleReward
    {
        public readonly int Gold;
        public readonly TimeSpan CreditedTime;
        public IdleReward(int gold, TimeSpan creditedTime) { Gold = gold; CreditedTime = creditedTime; }
    }

    public interface IClock { DateTimeOffset UtcNow { get; } }

    public sealed class SystemClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    public sealed class IdleRewardService
    {
        public static readonly TimeSpan MaximumIdleTime = TimeSpan.FromHours(8);
        private const int GoldPerMinutePerStage = 2;
        private readonly PlayerProfile profile;
        private readonly IClock clock;
        private readonly TimeSpan maximumIdleTime;

        public IdleRewardService(PlayerProfile profile, IClock clock, TimeSpan? maximumIdleTime = null)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.maximumIdleTime = maximumIdleTime ?? MaximumIdleTime;
        }

        public IdleReward Preview()
        {
            var now = clock.UtcNow;
            var lastClaim = new DateTimeOffset(profile.lastIdleClaimUtcTicks, TimeSpan.Zero);
            var elapsed = now - lastClaim;
            if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
            var credited = elapsed > maximumIdleTime ? maximumIdleTime : elapsed;
            var wholeMinutes = (int)credited.TotalMinutes;
            var gold = checked(wholeMinutes * GoldPerMinutePerStage * Math.Max(1, profile.highestStage));
            return new IdleReward(gold, TimeSpan.FromMinutes(wholeMinutes));
        }

        public IdleReward Claim()
        {
            var reward = Preview();
            if (reward.Gold > 0) new EconomyService(profile).Grant(CurrencyType.Gold, reward.Gold);
            profile.lastIdleClaimUtcTicks = clock.UtcNow.UtcDateTime.Ticks;
            return reward;
        }
    }
}
