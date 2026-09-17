using IdleHeroDefense.Infrastructure;
using IdleHeroDefense.Progression;
using UnityEngine;

namespace IdleHeroDefense.Presentation
{
    public sealed class ProfileController : MonoBehaviour
    {
        public static ProfileController Instance { get; private set; }
        public PlayerProfile Profile { get; private set; }
        public IdleReward PendingIdleReward => CreateIdleRewardService().Preview();

        private IProfileRepository repository;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            var clock = new SystemClock();
            repository = new PlayerPrefsProfileRepository(clock);
            Profile = repository.LoadOrCreate();
            if (Profile.heroes.Count == 0)
            {
                Profile.GetOrCreateHero("ember_knight");
                Profile.GetOrCreateHero("forest_archer");
                Profile.GetOrCreateHero("shade_mage");
                Profile.activeFormation.Add("ember_knight");
                Profile.activeFormation.Add("forest_archer");
                Profile.activeFormation.Add("shade_mage");
                repository.Save(Profile);
            }
            else if (Profile.activeFormation.Count == 0)
            {
                foreach (var hero in Profile.heroes)
                {
                    if (Profile.activeFormation.Count >= 5) break;
                    Profile.activeFormation.Add(hero.heroId);
                }
                repository.Save(Profile);
            }
            var equipment = new EquipmentService(Profile);
            if (Profile.equipment.Count == 0)
            {
                equipment.EnsureStarterEquipment();
                equipment.AutoEquip("ember_knight", out _);
                equipment.AutoEquip("forest_archer", out _);
                equipment.AutoEquip("shade_mage", out _);
                repository.Save(Profile);
            }
        }

        public int DailyAttemptsRemaining => new GameModeService(Profile, new SystemClock()).DailyAttemptsRemaining;
        public int DailyAttemptLimit => new GameModeService(Profile, new SystemClock()).DailyAttemptLimit;

        public int Energy => new EnergyService(Profile, new SystemClock()).Current;
        public System.TimeSpan EnergyTimeUntilNext => new EnergyService(Profile, new SystemClock()).TimeUntilNext;

        public bool SetFormation(System.Collections.Generic.IEnumerable<string> heroIds, out string reason)
        {
            var changed = new FormationService(Profile).TrySet(heroIds, out reason);
            if (changed)
            {
                Save();
                FindObjectOfType<BackendController>()?.RequestProfileSync();
            }
            return changed;
        }

        public bool AutoEquip(string heroId, out string reason)
        {
            var result = new EquipmentService(Profile).AutoEquip(heroId, out reason);
            if (result) Save();
            return result;
        }

        public TutorialStep TutorialStep => new TutorialService(Profile).Current;

        public bool AdvanceTutorial(TutorialAction action)
        {
            var advanced = new TutorialService(Profile).TryAdvance(action);
            if (advanced)
            {
                Save();
                AnalyticsService.Track("tutorial_step_completed", "action", action, "next_step", TutorialStep);
            }
            return advanced;
        }

        public void SaveSettings(bool music, bool sound, bool reducedMotion, bool damageText, bool haptics)
        {
            Profile.musicEnabled = music;
            Profile.soundEnabled = sound;
            Profile.reducedMotion = reducedMotion;
            Profile.showDamageText = damageText;
            Profile.hapticsEnabled = haptics;
            Save();
            AnalyticsService.Track("settings_changed", "music", music, "sound", sound,
                "reduced_motion", reducedMotion, "damage_text", damageText, "haptics", haptics);
        }

        public System.Collections.Generic.IReadOnlyList<RepeatableQuestView> DailyQuests =>
            new RepeatableQuestService(Profile, new SystemClock()).Daily();

        public System.Collections.Generic.IReadOnlyList<RepeatableQuestView> WeeklyQuests =>
            new RepeatableQuestService(Profile, new SystemClock()).Weekly();

        public System.Collections.Generic.IReadOnlyList<InboxMessage> GetInbox() =>
            new LiveOpsService(Profile, new SystemClock()).GetInbox(LiveConfigService.Current);

        public void Save() => repository.Save(Profile);

        private IdleRewardService CreateIdleRewardService() => new IdleRewardService(Profile, new SystemClock(),
            System.TimeSpan.FromHours(LiveConfigService.Current.idleCapHours));

        private void OnApplicationPause(bool paused) { if (paused) Save(); }
        private void OnApplicationFocus(bool focused) { if (!focused) Save(); }
        private void OnApplicationQuit() => Save();
    }
}
