using System;
using System.Collections.Generic;

namespace IdleHeroDefense.Progression
{
    public enum HeroRarity { Rare, Epic }

    public readonly struct SummonResult
    {
        public readonly bool Success;
        public readonly string Reason;
        public readonly string HeroId;
        public readonly HeroRarity Rarity;
        public readonly int Shards;
        public readonly bool WasPity;

        public SummonResult(bool success, string reason, string heroId = "", HeroRarity rarity = HeroRarity.Rare,
            int shards = 0, bool wasPity = false)
        {
            Success = success;
            Reason = reason;
            HeroId = heroId;
            Rarity = rarity;
            Shards = shards;
            WasPity = wasPity;
        }
    }

    public interface IRandomSource { int Range(int minimumInclusive, int maximumExclusive); }

    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly Random random = new Random();
        public int Range(int minimumInclusive, int maximumExclusive) => random.Next(minimumInclusive, maximumExclusive);
    }

    public sealed class SummonService
    {
        public const int GemCost = 100;
        public const int PityThreshold = 10;
        private static readonly string[] RarePool = { "forest_archer", "ember_knight", "iron_guard" };
        private static readonly string[] EpicPool = { "shade_mage", "sun_priest", "storm_hunter" };
        private readonly PlayerProfile profile;
        private readonly IRandomSource random;

        public SummonService(PlayerProfile profile, IRandomSource random)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public SummonResult SummonOne()
        {
            var spend = new EconomyService(profile).Spend(CurrencyType.Gems, GemCost);
            if (!spend.Success) return new SummonResult(false, spend.Reason);

            var pity = profile.summonPity + 1 >= PityThreshold;
            var epic = pity || random.Range(0, 100) < 10;
            var pool = epic ? EpicPool : RarePool;
            var heroId = pool[random.Range(0, pool.Length)];
            var shards = epic ? 30 : 10;
            profile.AddShards(heroId, shards);
            profile.GetOrCreateHero(heroId);
            profile.summonPity = epic ? 0 : profile.summonPity + 1;
            return new SummonResult(true, string.Empty, heroId, epic ? HeroRarity.Epic : HeroRarity.Rare, shards, pity);
        }
    }
}

