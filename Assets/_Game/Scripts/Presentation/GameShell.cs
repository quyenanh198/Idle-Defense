using System;
using IdleHeroDefense.Progression;
using IdleHeroDefense.Monetization;
using IdleHeroDefense.Infrastructure;
using UnityEngine;

namespace IdleHeroDefense.Presentation
{
    public sealed class GameShell : MonoBehaviour
    {
        private GUIStyle title;
        private GUIStyle heading;
        private GUIStyle body;
        private GUIStyle button;
        private GUIStyle selectedButton;
        private string toast = string.Empty;
        private float toastUntil;
        private bool showSettings;
        private Vector2 scrollPosition;

        private ProfileController Profiles => ProfileController.Instance;
        private BattleRunner Battles => FindObjectOfType<BattleRunner>();
        private MonetizationController Monetization => FindObjectOfType<MonetizationController>();
        private BackendController Backend => FindObjectOfType<BackendController>();

        private void OnGUI()
        {
            if (Profiles == null || Profiles.Profile == null) return;
            EnsureStyles();
            var safe = Screen.safeArea;
            var width = Mathf.Min(520f, safe.width - 24f);
            var x = safe.x + (safe.width - width) * 0.5f;
            var navHeight = 62f;

            if (AppNavigation.Current != AppScreen.Battle)
            {
                GUILayout.BeginArea(new Rect(x, safe.y + 12f, width, safe.height - navHeight - 30f), GUI.skin.box);
                DrawCurrencyHeader();
                scrollPosition = GUILayout.BeginScrollView(scrollPosition);
                if (showSettings) DrawSettings();
                else
                {
                GUILayout.Space(8);
                switch (AppNavigation.Current)
                {
                    case AppScreen.Home: DrawHome(); break;
                    case AppScreen.Heroes: DrawHeroes(); break;
                    case AppScreen.Summon: DrawSummon(); break;
                    case AppScreen.Shop: DrawShop(); break;
                }
                }
                GUILayout.EndScrollView();
                GUILayout.EndArea();
            }

            DrawBottomNavigation(new Rect(x, safe.yMax - navHeight - 4f, width, navHeight));
            if (!string.IsNullOrEmpty(toast) && Time.unscaledTime < toastUntil)
                GUI.Box(new Rect(x + 24f, safe.yMax - navHeight - 64f, width - 48f, 48f), toast);
        }

        private void DrawCurrencyHeader()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"PLAYER Lv.{Profiles.Profile.playerLevel}", heading);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"Gold {Profiles.Profile.gold:N0}   Gems {Profiles.Profile.gems:N0}", body);
            var energyTimer = Profiles.Energy >= EnergyService.MaximumEnergy ? "FULL" : $"+1 {FormatDuration(Profiles.EnergyTimeUntilNext)}";
            GUILayout.Label($"Energy {Profiles.Energy}/{EnergyService.MaximumEnergy} {energyTimer}", body);
            if (GUILayout.Button(showSettings ? L("common.close", "CLOSE") : L("common.settings", "SETTINGS"), GUILayout.Width(82))) showSettings = !showSettings;
            GUILayout.EndHorizontal();
        }

        private void DrawHome()
        {
            GUILayout.Label(L("game.title", "IDLE HERO DEFENSE"), title);
            if (Backend != null && !string.IsNullOrEmpty(Backend.ProgressionStatus)) GUILayout.Label(Backend.ProgressionStatus, body);
            if (Backend != null && !string.IsNullOrEmpty(Backend.RewardStatus)) GUILayout.Label(Backend.RewardStatus, body);
            DrawTutorialBanner();
            GUILayout.Space(12);
            GUILayout.Label("Your heroes kept defending while you were away.", body);
            var reward = Profiles.PendingIdleReward;
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(L("home.idle_chest", "IDLE CHEST"), heading);
            GUILayout.Label($"Stored time: {FormatDuration(reward.CreditedTime)}", body);
            GUILayout.Label($"Reward: {reward.Gold:N0} gold", body);
            GUI.enabled = reward.Gold > 0 && Backend != null && Backend.IsReady && !Backend.IsRewardBusy;
            if (GUILayout.Button(L("home.claim", "CLAIM REWARD"), button))
            {
                Backend.ClaimIdleReward();
            }
            GUI.enabled = true;
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();
            GUILayout.Label($"Campaign stage {Profiles.Profile.highestStage}", heading);
            if (GUILayout.Button(L("home.continue", "CONTINUE CAMPAIGN"), button)) StartMode(GameMode.Campaign);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"DAILY DUNGEON\n{Profiles.DailyAttemptsRemaining}/{Profiles.DailyAttemptLimit}", button)) StartMode(GameMode.DailyDungeon);
            if (GUILayout.Button($"ENDLESS TOWER\nFloor {Profiles.Profile.endlessTowerFloor}", button)) StartMode(GameMode.EndlessTower);
            GUILayout.EndHorizontal();
            DrawMetaProgression();
            GUILayout.Space(6);
            GUILayout.Label(L("home.achievements", "ACHIEVEMENTS"), heading);
            foreach (var quest in new QuestService(Profiles.Profile).GetQuests())
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label($"{quest.Title}  {quest.Current}/{quest.Target}", body);
                GUI.enabled = quest.CanClaim && Backend != null && Backend.IsReady && !Backend.IsProgressionBusy;
                if (GUILayout.Button(quest.Claimed ? "CLAIMED" : $"CLAIM +{quest.GoldReward}", button, GUILayout.Width(122)))
                {
                    Backend.ClaimAchievement(quest.Id);
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            DrawRepeatableQuests(L("home.daily_quests", "DAILY QUESTS"), Profiles.DailyQuests);
            DrawRepeatableQuests(L("home.weekly_quests", "WEEKLY QUESTS"), Profiles.WeeklyQuests);
            GUILayout.Space(6);
            GUILayout.Label(L("home.inbox", "INBOX"), heading);
            foreach (var mail in Profiles.GetInbox())
            {
                var claimed = Profiles.Profile.claimedMailIds.Contains(mail.Id);
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label($"{mail.Title}\n{mail.Gold} gold • {mail.Gems} gems", body);
                GUI.enabled = !claimed && Backend != null && Backend.IsReady && !Backend.IsProgressionBusy;
                if (GUILayout.Button(claimed ? L("common.claimed", "CLAIMED") : L("common.claim", "CLAIM"), button, GUILayout.Width(92)))
                {
                    Backend.ClaimMail(mail.Id);
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }

        private void DrawHeroes()
        {
            GUILayout.Label(L("heroes.title", "HEROES"), title);
            GUILayout.Label("Upgrade your active roster. Each level increases future combat scaling.", body);
            foreach (var progress in Profiles.Profile.heroes)
            {
                var definition = IdleHeroDefense.Configuration.HeroCatalog.Get(progress.heroId);
                DrawHeroCard(progress.heroId, definition.DisplayName, $"{definition.Class} • {definition.Faction}");
            }
            GUILayout.Space(6);
            GUILayout.Label(L("heroes.formation", "ACTIVE FORMATION"), heading);
            GUILayout.Label(string.Join("  •  ", Profiles.Profile.activeFormation), body);
            var activeDefinitions = new System.Collections.Generic.List<IdleHeroDefense.Domain.HeroDefinition>();
            foreach (var id in Profiles.Profile.activeFormation)
                activeDefinitions.Add(IdleHeroDefense.Configuration.HeroCatalog.Get(id));
            foreach (var bonus in FormationService.CalculateBonuses(activeDefinitions))
                GUILayout.Label($"{bonus.Faction} x{bonus.Count}: ATK +{(bonus.AttackMultiplier - 1f) * 100:0}%  HP +{(bonus.HealthMultiplier - 1f) * 100:0}%", body);
        }

        private void DrawHeroCard(string id, string displayName, string role)
        {
            var progress = Profiles.Profile.GetOrCreateHero(id);
            var cost = HeroUpgradeService.GoldCostForNextLevel(progress.level);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"{displayName}  •  Lv.{progress.level}", heading);
            GUILayout.Label(role, body);
            var equippedStats = new EquipmentService(Profiles.Profile).StatsFor(id);
            GUILayout.Label($"Equipment: +{equippedStats.attack} ATK  +{equippedStats.health} HP", body);
            GUILayout.BeginHorizontal();
            GUI.enabled = progress.level < HeroUpgradeService.MaximumLevel && Profiles.Profile.gold >= cost &&
                          Backend != null && Backend.IsReady && !Backend.IsEconomyBusy;
            if (GUILayout.Button(progress.level >= HeroUpgradeService.MaximumLevel ? "MAX LEVEL" : $"LEVEL UP  •  {cost} gold", button))
            {
                Backend.LevelUpHero(id);
            }
            GUI.enabled = true;
            if (GUILayout.Button("AUTO-EQUIP", button))
            {
                Profiles.AutoEquip(id, out var reason);
                ShowToast(string.IsNullOrEmpty(reason) ? "Best available gear equipped" : reason);
            }
            GUILayout.EndHorizontal();
            var active = Profiles.Profile.activeFormation.Contains(id);
            if (GUILayout.Button(active ? "REMOVE FROM FORMATION" : "ADD TO FORMATION", button)) ToggleFormation(id);
            foreach (var item in Profiles.Profile.equipment)
            {
                if (item.equippedHeroId != id) continue;
                var equipment = EquipmentCatalog.Get(item.definitionId);
                var upgradeCost = item.level * 25;
                GUI.enabled = Profiles.Profile.gearMaterials >= upgradeCost && Backend != null && Backend.IsReady && !Backend.IsProgressionBusy;
                if (GUILayout.Button($"{equipment.Name} Lv.{item.level}  •  Upgrade {upgradeCost} mats", button))
                {
                    Backend.UpgradeEquipment(item.instanceId);
                }
                GUI.enabled = true;
            }
            GUILayout.EndVertical();
        }

        private void DrawMetaProgression()
        {
            var meta = new MetaProgressionService(Profiles.Profile);
            GUILayout.Space(6);
            GUILayout.Label("BASE & ARTIFACTS", heading);
            GUILayout.BeginHorizontal();
            GUI.enabled = Profiles.Profile.gold >= meta.BaseUpgradeCost && Backend != null && Backend.IsReady && !Backend.IsProgressionBusy;
            if (GUILayout.Button($"Base Lv.{Profiles.Profile.baseLevel}\n{meta.BaseMaxHealth} HP • {meta.BaseUpgradeCost} gold", button))
            {
                Backend.UpgradeBase();
            }
            GUI.enabled = true;
            DrawArtifactButton("war_banner", "War Banner", "+3% hero ATK/lv");
            DrawArtifactButton("guardian_idol", "Guardian Idol", "+10% base HP/lv");
            GUILayout.EndHorizontal();
            GUILayout.Label($"Gear materials {Profiles.Profile.gearMaterials} • Artifact dust {Profiles.Profile.artifactDust}", body);
        }

        private void DrawArtifactButton(string id, string label, string effect)
        {
            var meta = new MetaProgressionService(Profiles.Profile);
            var level = meta.GetArtifactLevel(id);
            var cost = level * 50;
            GUI.enabled = Profiles.Profile.artifactDust >= cost && Backend != null && Backend.IsReady && !Backend.IsProgressionBusy;
            if (GUILayout.Button($"{label} Lv.{level}\n{effect} • {cost} dust", button))
            {
                Backend.UpgradeArtifact(id);
            }
            GUI.enabled = true;
        }

        private void ToggleFormation(string heroId)
        {
            var formation = new System.Collections.Generic.List<string>(Profiles.Profile.activeFormation);
            if (formation.Contains(heroId))
            {
                if (formation.Count == 1) { ShowToast("Formation needs at least one hero"); return; }
                formation.Remove(heroId);
            }
            else
            {
                if (formation.Count >= 5) { ShowToast("Formation is full"); return; }
                formation.Add(heroId);
            }
            ShowToast(Profiles.SetFormation(formation, out var reason) ? "Formation updated" : reason);
        }

        private void DrawSummon()
        {
            GUILayout.Label(L("summon.title", "SUMMON"), title);
            GUILayout.FlexibleSpace();
            GUILayout.Label("HERO PORTAL", heading);
            GUILayout.Label("Rare 90%  •  Epic 10%", body);
            GUILayout.Label($"Epic pity: {Profiles.Profile.summonPity}/{SummonService.PityThreshold}", body);
            if (Backend != null && !string.IsNullOrEmpty(Backend.SummonStatus)) GUILayout.Label(Backend.SummonStatus, body);
            GUI.enabled = Profiles.Profile.gems >= SummonService.GemCost && Backend != null && Backend.IsReady && !Backend.IsSummoning;
            if (GUILayout.Button($"SUMMON 1  •  {SummonService.GemCost} gems", button))
            {
                Backend.SummonOne();
            }
            GUI.enabled = true;
            GUILayout.Label("Every summon grants hero shards. An Epic resets pity; the 10th non-Epic pull is guaranteed Epic.", body);
            GUILayout.FlexibleSpace();
        }

        private void DrawShop()
        {
            GUILayout.Label(L("shop.title", "SHOP"), title);
            GUILayout.Label(Monetization == null ? "Monetization unavailable" : Monetization.Status, body);
            foreach (var product in ProductCatalog.Products)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label($"{product.DisplayName}\n{product.Gems} gems", body);
                GUI.enabled = Monetization != null && Monetization.StoreAvailable;
                if (GUILayout.Button("BUY", button, GUILayout.Width(90))) Monetization.Purchase(product.Id);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            GUI.enabled = Monetization != null && Monetization.StoreAvailable;
            if (GUILayout.Button("RESTORE PURCHASES", button)) Monetization.RestorePurchases();
            GUI.enabled = true;
            GUILayout.Space(10);
            GUILayout.Label("REWARDED AD", heading);
            GUILayout.Label($"Watch limit: {(Monetization == null ? 0 : Monetization.RewardedAdsRemaining)}/{RewardedAdService.DailyLimit} remaining", body);
            GUI.enabled = Monetization != null && Monetization.AdAvailable && Monetization.RewardedAdsRemaining > 0;
            if (GUILayout.Button($"WATCH • +{RewardedAdService.GemReward} GEMS", button)) Monetization.ShowRewardedAd();
            GUI.enabled = true;
        }

        private void DrawBottomNavigation(Rect rect)
        {
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.BeginHorizontal();
            DrawNavButton(L("nav.home", "HOME"), AppScreen.Home);
            DrawNavButton(L("nav.heroes", "HEROES"), AppScreen.Heroes);
            DrawNavButton(L("nav.battle", "BATTLE"), AppScreen.Battle);
            DrawNavButton(L("nav.summon", "SUMMON"), AppScreen.Summon);
            DrawNavButton(L("nav.shop", "SHOP"), AppScreen.Shop);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawNavButton(string label, AppScreen screen)
        {
            var feature = screen == AppScreen.Summon ? Feature.Summon : screen == AppScreen.Shop ? Feature.Shop : Feature.Heroes;
            var gated = (screen == AppScreen.Summon || screen == AppScreen.Shop) &&
                        !FeatureUnlockService.IsUnlocked(Profiles.Profile, feature);
            var style = AppNavigation.Current == screen ? selectedButton : button;
            GUI.enabled = !gated;
            if (GUILayout.Button(gated ? $"{label}\nS{FeatureUnlockService.RequiredStage(feature)}" : label, style, GUILayout.ExpandWidth(true)))
            {
                AppNavigation.GoTo(screen);
                if (screen == AppScreen.Heroes) Profiles.AdvanceTutorial(TutorialAction.HeroesOpened);
            }
            GUI.enabled = true;
        }

        private void ShowToast(string message)
        {
            toast = message;
            toastUntil = Time.unscaledTime + 2f;
        }

        private void StartMode(GameMode mode)
        {
            var feature = mode == GameMode.DailyDungeon ? Feature.DailyDungeon : Feature.EndlessTower;
            if (mode != GameMode.Campaign && !FeatureUnlockService.IsUnlocked(Profiles.Profile, feature))
            {
                ShowToast($"Unlocks at stage {FeatureUnlockService.RequiredStage(feature)}");
                return;
            }
            if (Battles == null || Backend == null) { ShowToast("Battle system is unavailable."); return; }
            Backend.StartBattle(mode);
        }

        private void DrawTutorialBanner()
        {
            var step = Profiles.TutorialStep;
            if (step == TutorialStep.Complete) return;
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"TUTORIAL • {step}", heading);
            var instruction = step == TutorialStep.Welcome ? "Welcome! Build your team and defend the base."
                : step == TutorialStep.OpenHeroes ? "Open Heroes from the navigation below."
                : step == TutorialStep.UpgradeHero ? "Upgrade any hero once."
                : step == TutorialStep.StartBattle ? "Return here and start the campaign."
                : "Win your first battle.";
            GUILayout.Label(instruction, body);
            if (step == TutorialStep.Welcome && GUILayout.Button("LET'S GO", button))
                Profiles.AdvanceTutorial(TutorialAction.Continue);
            GUILayout.EndVertical();
        }

        private void DrawSettings()
        {
            GUILayout.Label(L("settings.title", "SETTINGS & ACCESSIBILITY"), title);
            var profile = Profiles.Profile;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("English", button)) FindObjectOfType<LocalizationController>()?.SetLanguage("en");
            if (GUILayout.Button("Tiếng Việt", button)) FindObjectOfType<LocalizationController>()?.SetLanguage("vi");
            GUILayout.EndHorizontal();
            var music = GUILayout.Toggle(profile.musicEnabled, " " + L("settings.music", "Music"), button);
            var sound = GUILayout.Toggle(profile.soundEnabled, " " + L("settings.sound", "Sound effects"), button);
            var reducedMotion = GUILayout.Toggle(profile.reducedMotion, " " + L("settings.motion", "Reduce motion and flashes"), button);
            var damageText = GUILayout.Toggle(profile.showDamageText, " " + L("settings.damage", "Show combat damage text"), button);
            var haptics = GUILayout.Toggle(profile.hapticsEnabled, " " + L("settings.haptics", "Haptic feedback"), button);
            if (music != profile.musicEnabled || sound != profile.soundEnabled ||
                reducedMotion != profile.reducedMotion || damageText != profile.showDamageText || haptics != profile.hapticsEnabled)
                Profiles.SaveSettings(music, sound, reducedMotion, damageText, haptics);
        }

        private void DrawRepeatableQuests(string label, System.Collections.Generic.IReadOnlyList<RepeatableQuestView> quests)
        {
            GUILayout.Space(6);
            GUILayout.Label(label, heading);
            foreach (var quest in quests)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label($"{quest.Title} {quest.Current}/{quest.Target}\n+{quest.Gold} gold +{quest.Gems} gems", body);
                GUI.enabled = quest.CanClaim && Backend != null && Backend.IsReady && !Backend.IsRewardBusy;
                if (GUILayout.Button(quest.Claimed ? L("common.claimed", "CLAIMED") : L("common.claim", "CLAIM"), button, GUILayout.Width(92)))
                {
                    Backend.ClaimRepeatableQuest(quest.Id);
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }

        private static string FormatDuration(TimeSpan value) => $"{(int)value.TotalHours:00}:{value.Minutes:00}";

        private static string L(string key, string fallback) => LocalizationService.Get(key, fallback);

        private void EnsureStyles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            heading = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, wordWrap = true };
            body = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            button = new GUIStyle(GUI.skin.button) { fontSize = 14, fixedHeight = 44, wordWrap = true };
            selectedButton = new GUIStyle(button) { fontStyle = FontStyle.Bold };
            selectedButton.normal.textColor = new Color(1f, 0.78f, 0.2f);
        }
    }
}
