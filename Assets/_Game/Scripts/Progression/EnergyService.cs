using System;

namespace IdleHeroDefense.Progression
{
    public sealed class EnergyService
    {
        public const int MaximumEnergy = 60;
        public static readonly TimeSpan RegenerationInterval = TimeSpan.FromMinutes(5);
        private readonly PlayerProfile profile;
        private readonly IClock clock;

        public EnergyService(PlayerProfile profile, IClock clock)
        { this.profile = profile ?? throw new ArgumentNullException(nameof(profile)); this.clock = clock ?? throw new ArgumentNullException(nameof(clock)); }

        public int Current
        {
            get { Regenerate(); return profile.energy; }
        }

        public TimeSpan TimeUntilNext
        {
            get
            {
                Regenerate();
                if (profile.energy >= MaximumEnergy) return TimeSpan.Zero;
                var last = new DateTimeOffset(profile.lastEnergyUtcTicks, TimeSpan.Zero);
                var remaining = RegenerationInterval - (clock.UtcNow - last);
                return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
            }
        }

        public bool TrySpend(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            Regenerate();
            if (profile.energy < amount) return false;
            profile.energy -= amount;
            return true;
        }

        private void Regenerate()
        {
            var now = clock.UtcNow;
            if (profile.lastEnergyUtcTicks <= 0) profile.lastEnergyUtcTicks = now.UtcDateTime.Ticks;
            if (profile.energy >= MaximumEnergy) { profile.energy = MaximumEnergy; profile.lastEnergyUtcTicks = now.UtcDateTime.Ticks; return; }
            var last = new DateTimeOffset(profile.lastEnergyUtcTicks, TimeSpan.Zero);
            var elapsed = now - last;
            if (elapsed <= TimeSpan.Zero) return;
            var recovered = (int)(elapsed.Ticks / RegenerationInterval.Ticks);
            if (recovered <= 0) return;
            profile.energy = Math.Min(MaximumEnergy, profile.energy + recovered);
            profile.lastEnergyUtcTicks = last.AddTicks(RegenerationInterval.Ticks * recovered).UtcDateTime.Ticks;
            if (profile.energy >= MaximumEnergy) profile.lastEnergyUtcTicks = now.UtcDateTime.Ticks;
        }
    }
}

