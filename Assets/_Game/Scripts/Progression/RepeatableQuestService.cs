using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace IdleHeroDefense.Progression
{
    public readonly struct RepeatableQuestView
    {
        public readonly string Id;
        public readonly string Title;
        public readonly int Current;
        public readonly int Target;
        public readonly int Gold;
        public readonly int Gems;
        public readonly bool Claimed;
        public bool CanClaim => !Claimed && Current >= Target;
        public RepeatableQuestView(string id, string title, int current, int target, int gold, int gems, bool claimed)
        { Id = id; Title = title; Current = current; Target = target; Gold = gold; Gems = gems; Claimed = claimed; }
    }

    public readonly struct RepeatableQuestReward
    {
        public readonly bool Success;
        public readonly string Error;
        public readonly int Gold;
        public readonly int Gems;
        public RepeatableQuestReward(bool success, string error, int gold, int gems)
        { Success = success; Error = error; Gold = gold; Gems = gems; }
    }

    public sealed class RepeatableQuestService
    {
        private readonly PlayerProfile profile;
        private readonly IClock clock;
        public RepeatableQuestService(PlayerProfile profile, IClock clock)
        { this.profile = profile ?? throw new ArgumentNullException(nameof(profile)); this.clock = clock ?? throw new ArgumentNullException(nameof(clock)); }

        public void RecordBattleWin() { Refresh(); profile.dailyBattleWins++; profile.weeklyBattleWins++; }
        public void RecordHeroUpgrade() { Refresh(); profile.dailyHeroUpgrades++; }
        public void RecordSummon() { Refresh(); profile.dailySummons++; }

        public IReadOnlyList<RepeatableQuestView> Daily()
        {
            Refresh();
            return new[]
            {
                View("daily_win", "Win 1 battle", profile.dailyBattleWins, 1, 75, 0, profile.dailyClaimedQuestIds),
                View("daily_upgrade", "Upgrade a hero", profile.dailyHeroUpgrades, 1, 75, 0, profile.dailyClaimedQuestIds),
                View("daily_summon", "Summon 1 hero", profile.dailySummons, 1, 0, 10, profile.dailyClaimedQuestIds)
            };
        }

        public IReadOnlyList<RepeatableQuestView> Weekly()
        {
            Refresh();
            return new[] { View("weekly_wins", "Win 10 battles", profile.weeklyBattleWins, 10, 500, 50, profile.weeklyClaimedQuestIds) };
        }

        public RepeatableQuestReward Claim(string id)
        {
            var quest = Daily().Concat(Weekly()).FirstOrDefault(x => x.Id == id);
            if (string.IsNullOrEmpty(quest.Id)) return new RepeatableQuestReward(false, "Quest not found.", 0, 0);
            if (!quest.CanClaim) return new RepeatableQuestReward(false, quest.Claimed ? "Quest already claimed." : "Quest is not complete.", 0, 0);
            var claims = id.StartsWith("daily_", StringComparison.Ordinal) ? profile.dailyClaimedQuestIds : profile.weeklyClaimedQuestIds;
            claims.Add(id);
            var economy = new EconomyService(profile);
            if (quest.Gold > 0) economy.Grant(CurrencyType.Gold, quest.Gold);
            if (quest.Gems > 0) economy.Grant(CurrencyType.Gems, quest.Gems);
            return new RepeatableQuestReward(true, string.Empty, quest.Gold, quest.Gems);
        }

        private void Refresh()
        {
            var now = clock.UtcNow;
            var day = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (profile.dailyQuestUtcDate != day)
            {
                profile.dailyQuestUtcDate = day; profile.dailyBattleWins = 0; profile.dailyHeroUpgrades = 0; profile.dailySummons = 0;
                profile.dailyClaimedQuestIds.Clear();
            }
            var daysFromMonday = ((int)now.DayOfWeek + 6) % 7;
            var week = now.Date.AddDays(-daysFromMonday).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (profile.weeklyQuestUtcMonday != week)
            {
                profile.weeklyQuestUtcMonday = week; profile.weeklyBattleWins = 0; profile.weeklyClaimedQuestIds.Clear();
            }
        }

        private static RepeatableQuestView View(string id, string title, int current, int target, int gold, int gems, ICollection<string> claims) =>
            new RepeatableQuestView(id, title, Math.Min(current, target), target, gold, gems, claims.Contains(id));
    }
}

