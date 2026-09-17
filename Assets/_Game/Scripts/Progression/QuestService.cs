using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleHeroDefense.Progression
{
    public readonly struct QuestView
    {
        public readonly string Id;
        public readonly string Title;
        public readonly int Current;
        public readonly int Target;
        public readonly int GoldReward;
        public readonly bool Claimed;
        public bool CanClaim => !Claimed && Current >= Target;

        public QuestView(string id, string title, int current, int target, int goldReward, bool claimed)
        {
            Id = id; Title = title; Current = current; Target = target; GoldReward = goldReward; Claimed = claimed;
        }
    }

    public sealed class QuestService
    {
        private readonly PlayerProfile profile;
        public QuestService(PlayerProfile profile) => this.profile = profile ?? throw new ArgumentNullException(nameof(profile));

        public IReadOnlyList<QuestView> GetQuests()
        {
            var highestHeroLevel = profile.heroes.Count == 0 ? 1 : profile.heroes.Max(x => x.level);
            return new[]
            {
                View("first_win", "Win 1 battle", profile.totalBattleWins, 1, 100),
                View("stage_five", "Reach campaign stage 5", profile.highestStage, 5, 250),
                View("hero_level_three", "Raise a hero to level 3", highestHeroLevel, 3, 150)
            };
        }

        public EconomyResult Claim(string questId)
        {
            var quest = GetQuests().FirstOrDefault(x => x.Id == questId);
            if (string.IsNullOrEmpty(quest.Id)) return new EconomyResult(false, "Quest not found.", profile.gold);
            if (quest.Claimed) return new EconomyResult(false, "Quest already claimed.", profile.gold);
            if (!quest.CanClaim) return new EconomyResult(false, "Quest is not complete.", profile.gold);
            profile.claimedQuestIds.Add(quest.Id);
            return new EconomyService(profile).Grant(CurrencyType.Gold, quest.GoldReward);
        }

        private QuestView View(string id, string title, int current, int target, int reward) =>
            new QuestView(id, title, Math.Min(current, target), target, reward, profile.claimedQuestIds.Contains(id));
    }
}

