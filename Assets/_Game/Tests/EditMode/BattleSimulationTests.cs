using System;
using System.Collections.Generic;
using IdleHeroDefense.Domain;
using IdleHeroDefense.Progression;
using IdleHeroDefense.Infrastructure;
using IdleHeroDefense.Monetization;
using NUnit.Framework;

namespace IdleHeroDefense.Tests
{
    public sealed class BattleSimulationTests
    {
        private static HeroDefinition Hero(int attack = 20, int ultimateDamage = 100) =>
            new HeroDefinition("knight", "Knight", HeroClass.Warrior, Faction.Light, 100, attack, 1f, ultimateDamage);

        [Test]
        public void Start_SpawnsFirstWave()
        {
            var battle = NewBattle(new[] { new EnemyDefinition("slime", 50, 1, 2f) });
            battle.Start();
            Assert.That(battle.State, Is.EqualTo(BattleState.Running));
            Assert.That(battle.CurrentWave, Is.EqualTo(1));
            Assert.That(battle.Enemies, Has.Count.EqualTo(1));
        }

        [Test]
        public void Tick_WhenLastEnemyDies_CompletesWithVictory()
        {
            var battle = NewBattle(new[] { new EnemyDefinition("slime", 10, 0, 2f) });
            battle.Start();
            battle.Tick(1f);
            Assert.That(battle.State, Is.EqualTo(BattleState.Victory));
        }

        [Test]
        public void Tick_AdvancesAcrossMultipleWaves()
        {
            var waves = new IReadOnlyList<EnemyDefinition>[]
            {
                new[] { new EnemyDefinition("slime", 10, 0, 2f) },
                new[] { new EnemyDefinition("boss", 10, 0, 2f, true) }
            };
            var battle = new BattleSimulation(new[] { Hero() }, waves, 100);
            battle.Start();
            battle.Tick(1f);
            Assert.That(battle.CurrentWave, Is.EqualTo(2));
            battle.Tick(1f);
            Assert.That(battle.State, Is.EqualTo(BattleState.Victory));
        }

        [Test]
        public void Ultimate_RequiresEnergyAndConsumesIt()
        {
            var battle = NewBattle(new[] { new EnemyDefinition("boss", 500, 0, 2f, true) });
            battle.Start();
            Assert.That(battle.UseUltimate("knight"), Is.False);
            battle.Heroes[0].SetEnergyForTesting(100);
            Assert.That(battle.UseUltimate("knight"), Is.True);
            Assert.That(battle.Heroes[0].Energy, Is.Zero);
            Assert.That(battle.Enemies[0].Health, Is.EqualTo(400));
        }

        [Test]
        public void Enemies_DestroyBase_WhenAllHeroesAreDead()
        {
            var battle = new BattleSimulation(
                new[] { new HeroDefinition("weak", "Weak", HeroClass.Support, Faction.Nature, 1, 0, 1f, 0) },
                new[] { (IReadOnlyList<EnemyDefinition>)new[] { new EnemyDefinition("orc", 100, 10, 1f) } }, 10);
            battle.Start();
            battle.Tick(1f);
            battle.Tick(1f);
            Assert.That(battle.State, Is.EqualTo(BattleState.Defeat));
        }

        [Test]
        public void AreaUltimate_DamagesEveryLivingEnemyAndTracksStatistics()
        {
            var ability = new AbilityDefinition("nova", 25, TargetingRule.AllEnemies);
            var hero = new HeroDefinition("mage", "Mage", HeroClass.Mage, Faction.Shadow, 100, 0, 1f, 25, 100, ability);
            var wave = new[] { new EnemyDefinition("a", 100, 0, 2f), new EnemyDefinition("b", 100, 0, 2f) };
            var battle = new BattleSimulation(new[] { hero }, new[] { (IReadOnlyList<EnemyDefinition>)wave }, 100);
            battle.Start();
            battle.Heroes[0].SetEnergyForTesting(100);
            Assert.That(battle.UseUltimate("mage"), Is.True);
            Assert.That(battle.Enemies[0].Health, Is.EqualTo(75));
            Assert.That(battle.Enemies[1].Health, Is.EqualTo(75));
            Assert.That(battle.Statistics.DamageFor("mage"), Is.EqualTo(50));
        }

        [Test]
        public void Burn_DealsPeriodicDamageForConfiguredDuration()
        {
            var burn = new StatusEffectDefinition(StatusEffectType.Burn, 3f, 10, 1f);
            var ability = new AbilityDefinition("burn", 0, TargetingRule.FirstEnemy, burn);
            var hero = new HeroDefinition("mage", "Mage", HeroClass.Mage, Faction.Shadow, 100, 0, 10f, 0, 100, ability);
            var battle = new BattleSimulation(new[] { hero },
                new[] { (IReadOnlyList<EnemyDefinition>)new[] { new EnemyDefinition("dummy", 100, 0, 10f) } }, 100);
            battle.Start();
            battle.Heroes[0].SetEnergyForTesting(100);
            battle.UseUltimate("mage");
            battle.Tick(3f);
            Assert.That(battle.Enemies[0].Health, Is.EqualTo(70));
            Assert.That(battle.Statistics.DamageFor("mage"), Is.EqualTo(30));
        }

        [Test]
        public void Stun_PreventsEnemyAttackWhileActive()
        {
            var stun = new StatusEffectDefinition(StatusEffectType.Stun, 2f);
            var ability = new AbilityDefinition("stun", 0, TargetingRule.FirstEnemy, stun);
            var hero = new HeroDefinition("tank", "Tank", HeroClass.Tank, Faction.Light, 100, 0, 10f, 0, 100, ability);
            var battle = new BattleSimulation(new[] { hero },
                new[] { (IReadOnlyList<EnemyDefinition>)new[] { new EnemyDefinition("orc", 100, 10, 1f) } }, 100);
            battle.Start();
            battle.Heroes[0].SetEnergyForTesting(100);
            battle.UseUltimate("tank");
            battle.Tick(1f);
            Assert.That(battle.Heroes[0].Unit.Health, Is.EqualTo(100));
        }

        private static BattleSimulation NewBattle(IReadOnlyList<EnemyDefinition> wave) =>
            new BattleSimulation(new[] { Hero() }, new[] { wave }, 100);
    }

    public sealed class ProgressionTests
    {
        private sealed class CountingHaptics : IHapticProvider
        {
            public int Pulses;
            public void Pulse() => Pulses++;
        }
        private sealed class CapturingAnalyticsTransport : IAnalyticsTransport
        {
            public AnalyticsBatch Batch;
            public System.Threading.Tasks.Task SendAsync(AnalyticsBatch batch, System.Threading.CancellationToken cancellationToken)
            { Batch = batch; return System.Threading.Tasks.Task.CompletedTask; }
        }

        private sealed class FailingAnalyticsTransport : IAnalyticsTransport
        {
            public System.Threading.Tasks.Task SendAsync(AnalyticsBatch batch, System.Threading.CancellationToken cancellationToken) =>
                System.Threading.Tasks.Task.FromException(new InvalidOperationException("offline"));
        }
        private sealed class AcceptingReceiptValidator : IReceiptValidator
        {
            public System.Threading.Tasks.Task<ValidationResult> ValidateAsync(StorePurchase purchase,
                System.Threading.CancellationToken cancellationToken) =>
                System.Threading.Tasks.Task.FromResult(new ValidationResult { valid = true, transactionId = purchase.transactionId });
        }
        private sealed class FixedRandom : IRandomSource
        {
            private readonly int value;
            public FixedRandom(int value) => this.value = value;
            public int Range(int minimumInclusive, int maximumExclusive) => Math.Min(maximumExclusive - 1, Math.Max(minimumInclusive, value));
        }
        private sealed class FixedClock : IClock
        {
            public DateTimeOffset UtcNow { get; set; }
        }

        [Test]
        public void IdleReward_IsCappedAtEightHours()
        {
            var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var profile = PlayerProfile.CreateNew("player", start);
            profile.highestStage = 2;
            var clock = new FixedClock { UtcNow = start.AddHours(12) };
            var reward = new IdleRewardService(profile, clock).Claim();
            Assert.That(reward.CreditedTime, Is.EqualTo(TimeSpan.FromHours(8)));
            Assert.That(reward.Gold, Is.EqualTo(1920));
            Assert.That(profile.gold, Is.EqualTo(2420));
        }

        [Test]
        public void LevelUp_SpendsGoldAndIncreasesLevel()
        {
            var profile = PlayerProfile.CreateNew("player", DateTimeOffset.UtcNow);
            var result = new HeroUpgradeService(profile).LevelUp("knight");
            Assert.That(result.Success, Is.True);
            Assert.That(profile.GetOrCreateHero("knight").level, Is.EqualTo(2));
            Assert.That(profile.gold, Is.EqualTo(450));
        }

        [Test]
        public void LevelUp_DoesNotMutateWhenFundsAreInsufficient()
        {
            var profile = PlayerProfile.CreateNew("player", DateTimeOffset.UtcNow);
            profile.gold = 0;
            var result = new HeroUpgradeService(profile).LevelUp("knight");
            Assert.That(result.Success, Is.False);
            Assert.That(profile.GetOrCreateHero("knight").level, Is.EqualTo(1));
        }

        [Test]
        public void Summon_TenthPullGuaranteesEpicAndResetsPity()
        {
            var profile = PlayerProfile.CreateNew("player", DateTimeOffset.UtcNow);
            profile.summonPity = 9;
            var result = new SummonService(profile, new FixedRandom(99)).SummonOne();
            Assert.That(result.Success, Is.True);
            Assert.That(result.Rarity, Is.EqualTo(HeroRarity.Epic));
            Assert.That(result.WasPity, Is.True);
            Assert.That(profile.summonPity, Is.Zero);
            Assert.That(profile.gems, Is.Zero);
            Assert.That(profile.GetShards(result.HeroId), Is.EqualTo(30));
        }

        [Test]
        public void Quest_CannotBeClaimedTwice()
        {
            var profile = PlayerProfile.CreateNew("player", DateTimeOffset.UtcNow);
            profile.totalBattleWins = 1;
            var quests = new QuestService(profile);
            Assert.That(quests.Claim("first_win").Success, Is.True);
            Assert.That(quests.Claim("first_win").Success, Is.False);
            Assert.That(profile.gold, Is.EqualTo(600));
        }

        [Test]
        public void Formation_ThreeSameFactionGrantsAttackAndHealthBonus()
        {
            var heroes = new[]
            {
                new HeroDefinition("a", "A", HeroClass.Tank, Faction.Light, 100, 100, 1f, 100),
                new HeroDefinition("b", "B", HeroClass.Mage, Faction.Light, 100, 100, 1f, 100),
                new HeroDefinition("c", "C", HeroClass.Support, Faction.Light, 100, 100, 1f, 100)
            };
            var adjusted = FormationService.ApplyBonuses(heroes);
            Assert.That(adjusted[0].Attack, Is.EqualTo(110));
            Assert.That(adjusted[0].MaxHealth, Is.EqualTo(120));
        }

        [Test]
        public void Formation_RejectsDuplicateHeroes()
        {
            var profile = PlayerProfile.CreateNew("player", DateTimeOffset.UtcNow);
            profile.GetOrCreateHero("a");
            var result = new FormationService(profile).TrySet(new[] { "a", "a" }, out var reason);
            Assert.That(result, Is.False);
            Assert.That(reason, Is.Not.Empty);
        }

        [Test]
        public void DailyDungeon_AllowsThreeVictoriesPerUtcDay()
        {
            var clock = new FixedClock { UtcNow = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero) };
            var profile = PlayerProfile.CreateNew("player", clock.UtcNow);
            var modes = new GameModeService(profile, clock);
            for (var i = 0; i < 3; i++) modes.CompleteVictory(GameMode.DailyDungeon);
            Assert.That(modes.DailyAttemptsRemaining, Is.Zero);
            Assert.That(modes.CanStart(GameMode.DailyDungeon, out _), Is.False);
            clock.UtcNow = clock.UtcNow.AddDays(1);
            Assert.That(modes.DailyAttemptsRemaining, Is.EqualTo(3));
        }

        [Test]
        public void LiveConfig_RejectsInvalidEconomyValues()
        {
            var json = "{\"schemaVersion\":1,\"campaignGold\":-1,\"dailyDungeonAttempts\":3,\"idleCapHours\":8}";
            Assert.That(LiveConfigService.TryApplyJson(json, out var error), Is.False);
            Assert.That(error, Is.Not.Empty);
            LiveConfigService.Reset();
        }

        [Test]
        public void InboxClaim_IsIdempotent()
        {
            var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var clock = new FixedClock { UtcNow = now };
            var profile = PlayerProfile.CreateNew("player", now);
            var inbox = new LiveOpsService(profile, clock);
            var mail = new InboxMessage("gift", "Gift", 50, 5, now.AddDays(1));
            Assert.That(inbox.Claim(mail).Success, Is.True);
            Assert.That(inbox.Claim(mail).Success, Is.False);
            Assert.That(profile.gold, Is.EqualTo(550));
            Assert.That(profile.gems, Is.EqualTo(105));
        }

        [Test]
        public void LiveEventMail_IsVisibleOnlyInsideUtcWindow()
        {
            var now = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero);
            var clock = new FixedClock { UtcNow = now };
            var profile = PlayerProfile.CreateNew("player", now);
            var config = LiveConfig.Defaults();
            config.events.Add(new LiveEventConfig
            {
                id = "new_year", title = "New Year", startsUtc = "2026-01-01T00:00:00Z",
                endsUtc = "2026-01-03T00:00:00Z", mailGold = 100
            });
            var messages = new LiveOpsService(profile, clock).GetInbox(config);
            Assert.That(messages, Has.Count.EqualTo(2));
            clock.UtcNow = now.AddDays(2);
            Assert.That(new LiveOpsService(profile, clock).GetInbox(config), Has.Count.EqualTo(1));
        }

        [Test]
        public void OfflineBackend_ReplaysTransactionWithoutApplyingTwice()
        {
            var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var profile = PlayerProfile.CreateNew("player", now);
            var backend = new OfflineGameBackend(profile, new FixedClock { UtcNow = now });
            var session = backend.AuthenticateGuestAsync("install", System.Threading.CancellationToken.None).Result;
            var request = new EconomyTransactionRequest
            {
                requestId = "reward-1", operation = EconomyOperation.Grant,
                currency = CurrencyType.Gold, amount = 100, reason = "stage"
            };
            var first = backend.ExecuteEconomyAsync(session, request, System.Threading.CancellationToken.None).Result;
            var replay = backend.ExecuteEconomyAsync(session, request, System.Threading.CancellationToken.None).Result;
            Assert.That(first.success, Is.True);
            Assert.That(replay.wasReplay, Is.True);
            Assert.That(profile.gold, Is.EqualTo(600));
            Assert.That(profile.transactionReceipts, Has.Count.EqualTo(1));
        }

        [Test]
        public void OfflineBackend_RejectsIdempotencyKeyCollision()
        {
            var now = DateTimeOffset.UtcNow;
            var profile = PlayerProfile.CreateNew("player", now);
            var backend = new OfflineGameBackend(profile, new FixedClock { UtcNow = now });
            var session = backend.AuthenticateGuestAsync("install", System.Threading.CancellationToken.None).Result;
            var first = new EconomyTransactionRequest { requestId = "same", operation = EconomyOperation.Grant, currency = CurrencyType.Gold, amount = 10, reason = "a" };
            var collision = new EconomyTransactionRequest { requestId = "same", operation = EconomyOperation.Grant, currency = CurrencyType.Gold, amount = 99, reason = "b" };
            backend.ExecuteEconomyAsync(session, first, System.Threading.CancellationToken.None).Wait();
            var response = backend.ExecuteEconomyAsync(session, collision, System.Threading.CancellationToken.None).Result;
            Assert.That(response.success, Is.False);
            Assert.That(response.error, Does.Contain("collision"));
            Assert.That(profile.gold, Is.EqualTo(510));
        }

        [Test]
        public void Tutorial_OnlyAdvancesForExpectedAction()
        {
            var profile = PlayerProfile.CreateNew("player", DateTimeOffset.UtcNow);
            var tutorial = new TutorialService(profile);
            Assert.That(tutorial.TryAdvance(TutorialAction.HeroUpgraded), Is.False);
            Assert.That(tutorial.TryAdvance(TutorialAction.Continue), Is.True);
            Assert.That(tutorial.Current, Is.EqualTo(TutorialStep.OpenHeroes));
        }

        [Test]
        public void Features_UnlockAtConfiguredCampaignStages()
        {
            var profile = PlayerProfile.CreateNew("player", DateTimeOffset.UtcNow);
            Assert.That(FeatureUnlockService.IsUnlocked(profile, Feature.Summon), Is.False);
            profile.highestStage = 3;
            Assert.That(FeatureUnlockService.IsUnlocked(profile, Feature.Summon), Is.True);
            Assert.That(FeatureUnlockService.IsUnlocked(profile, Feature.EndlessTower), Is.False);
        }

        [Test]
        public void AutoEquip_AssignsOneBestItemPerSlot()
        {
            var profile = PlayerProfile.CreateNew("player", DateTimeOffset.UtcNow);
            profile.GetOrCreateHero("hero");
            var equipment = new EquipmentService(profile);
            equipment.EnsureStarterEquipment();
            Assert.That(equipment.AutoEquip("hero", out _), Is.True);
            Assert.That(profile.equipment.FindAll(x => x.equippedHeroId == "hero"), Has.Count.EqualTo(2));
            var stats = equipment.StatsFor("hero");
            Assert.That(stats.attack, Is.GreaterThan(0));
            Assert.That(stats.health, Is.GreaterThan(0));
        }

        [Test]
        public void EquipmentUpgrade_ConsumesMaterialsAndRaisesStats()
        {
            var profile = PlayerProfile.CreateNew("player", DateTimeOffset.UtcNow);
            profile.GetOrCreateHero("hero");
            var equipment = new EquipmentService(profile);
            equipment.EnsureStarterEquipment();
            equipment.AutoEquip("hero", out _);
            var item = profile.equipment.Find(x => x.equippedHeroId == "hero");
            var before = equipment.StatsFor("hero");
            Assert.That(equipment.Upgrade(item.instanceId).Success, Is.True);
            var after = equipment.StatsFor("hero");
            Assert.That(after.attack + after.health, Is.GreaterThan(before.attack + before.health));
            Assert.That(profile.gearMaterials, Is.EqualTo(175));
        }

        [Test]
        public void MetaProgression_UpgradesBaseAndArtifacts()
        {
            var profile = PlayerProfile.CreateNew("player", DateTimeOffset.UtcNow);
            var meta = new MetaProgressionService(profile);
            Assert.That(meta.UpgradeBase().Success, Is.True);
            Assert.That(meta.BaseMaxHealth, Is.EqualTo(600));
            Assert.That(meta.UpgradeArtifact("war_banner").Success, Is.True);
            Assert.That(meta.HeroAttackMultiplier, Is.EqualTo(1.06f).Within(0.001f));
            Assert.That(profile.artifactDust, Is.EqualTo(50));
        }

        [Test]
        public void GameModes_GrantTheirProgressionMaterials()
        {
            var now = DateTimeOffset.UtcNow;
            var profile = PlayerProfile.CreateNew("player", now);
            var modes = new GameModeService(profile, new FixedClock { UtcNow = now });
            modes.CompleteVictory(GameMode.DailyDungeon);
            modes.CompleteVictory(GameMode.EndlessTower);
            Assert.That(profile.gearMaterials, Is.EqualTo(250));
            Assert.That(profile.artifactDust, Is.EqualTo(125));
        }

        [Test]
        public void ValidatedPurchase_GrantsExactlyOnce()
        {
            var profile = PlayerProfile.CreateNew("player", DateTimeOffset.UtcNow);
            var service = new MonetizationService(profile, new AcceptingReceiptValidator());
            var purchase = new StorePurchase { productId = "gems_500", transactionId = "tx-1", receipt = "signed", store = "test" };
            var first = service.ValidateAndGrantAsync(purchase, System.Threading.CancellationToken.None).Result;
            var replay = service.ValidateAndGrantAsync(purchase, System.Threading.CancellationToken.None).Result;
            Assert.That(first.Success, Is.True);
            Assert.That(replay.WasReplay, Is.True);
            Assert.That(profile.gems, Is.EqualTo(600));
        }

        [Test]
        public void RewardedAd_StopsAtDailyLimitAndResetsNextDay()
        {
            var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var clock = new FixedClock { UtcNow = now };
            var profile = PlayerProfile.CreateNew("player", now);
            var rewards = new RewardedAdService(profile, clock);
            for (var i = 0; i < RewardedAdService.DailyLimit; i++) Assert.That(rewards.GrantCompletedView(), Is.True);
            Assert.That(rewards.GrantCompletedView(), Is.False);
            Assert.That(profile.gems, Is.EqualTo(150));
            clock.UtcNow = now.AddDays(1);
            Assert.That(rewards.Remaining, Is.EqualTo(RewardedAdService.DailyLimit));
        }

        [Test]
        public void Energy_RegeneratesWholeIntervalsAndCapsAtMaximum()
        {
            var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var clock = new FixedClock { UtcNow = now };
            var profile = PlayerProfile.CreateNew("player", now);
            var energy = new EnergyService(profile, clock);
            Assert.That(energy.TrySpend(10), Is.True);
            clock.UtcNow = now.AddMinutes(49);
            Assert.That(energy.Current, Is.EqualTo(59));
            Assert.That(energy.TimeUntilNext, Is.EqualTo(TimeSpan.FromMinutes(1)));
            clock.UtcNow = now.AddMinutes(50);
            Assert.That(energy.Current, Is.EqualTo(EnergyService.MaximumEnergy));
        }

        [Test]
        public void GameModeStart_SpendsEnergyOnlyForLimitedModes()
        {
            var now = DateTimeOffset.UtcNow;
            var clock = new FixedClock { UtcNow = now };
            var profile = PlayerProfile.CreateNew("player", now);
            var modes = new GameModeService(profile, clock);
            Assert.That(modes.TryStart(GameMode.Campaign, out _), Is.True);
            Assert.That(profile.energy, Is.EqualTo(60));
            Assert.That(modes.TryStart(GameMode.DailyDungeon, out _), Is.True);
            Assert.That(profile.energy, Is.EqualTo(55));
            Assert.That(modes.TryStart(GameMode.EndlessTower, out _), Is.True);
            Assert.That(profile.energy, Is.EqualTo(52));
        }

        [Test]
        public void DailyQuest_ResetsProgressAndClaimOnNextUtcDay()
        {
            var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
            var clock = new FixedClock { UtcNow = now };
            var profile = PlayerProfile.CreateNew("player", now);
            var quests = new RepeatableQuestService(profile, clock);
            quests.RecordBattleWin();
            var claimed = quests.Claim("daily_win");
            Assert.That(claimed.Success, Is.True);
            Assert.That(profile.gold, Is.EqualTo(575));
            clock.UtcNow = now.AddDays(1);
            var reset = quests.Daily()[0];
            Assert.That(reset.Current, Is.Zero);
            Assert.That(reset.Claimed, Is.False);
        }

        [Test]
        public void WeeklyQuest_RetainsWithinWeekAndResetsOnMonday()
        {
            var friday = new DateTimeOffset(2026, 1, 2, 12, 0, 0, TimeSpan.Zero);
            var clock = new FixedClock { UtcNow = friday };
            var profile = PlayerProfile.CreateNew("player", friday);
            var quests = new RepeatableQuestService(profile, clock);
            quests.RecordBattleWin();
            clock.UtcNow = friday.AddDays(2);
            Assert.That(quests.Weekly()[0].Current, Is.EqualTo(1));
            clock.UtcNow = friday.AddDays(3);
            Assert.That(quests.Weekly()[0].Current, Is.Zero);
        }

        [Test]
        public void BufferedAnalytics_FlushesStructuredBatchAndRemovesEvents()
        {
            var transport = new CapturingAnalyticsTransport();
            var sink = new BufferedAnalyticsSink(transport, false, false);
            sink.Track("stage_started", new System.Collections.Generic.Dictionary<string, object> { ["stage"] = 3 });
            Assert.That(sink.FlushAsync(System.Threading.CancellationToken.None).Result, Is.True);
            Assert.That(transport.Batch.events, Has.Count.EqualTo(1));
            Assert.That(transport.Batch.events[0].name, Is.EqualTo("stage_started"));
            Assert.That(transport.Batch.events[0].properties[0].value, Is.EqualTo("3"));
            Assert.That(sink.PendingCount, Is.Zero);
        }

        [Test]
        public void BufferedAnalytics_RetainsEventsWhenTransportFails()
        {
            var sink = new BufferedAnalyticsSink(new FailingAnalyticsTransport(), false, false);
            sink.Track("event", new System.Collections.Generic.Dictionary<string, object>());
            Assert.That(sink.FlushAsync(System.Threading.CancellationToken.None).Result, Is.False);
            Assert.That(sink.PendingCount, Is.EqualTo(1));
        }

        [Test]
        public void Localization_LoadsVietnameseAndFallsBackToEnglish()
        {
            Assert.That(LocalizationService.SetLanguage("vi"), Is.True);
            Assert.That(LocalizationService.Get("nav.battle"), Is.EqualTo("CHIẾN ĐẤU"));
            Assert.That(LocalizationService.Get("missing.key", "Fallback"), Is.EqualTo("Fallback"));
            Assert.That(LocalizationService.SetLanguage("not-a-language"), Is.True);
            Assert.That(LocalizationService.Language, Is.EqualTo("en"));
            Assert.That(LocalizationService.Get("nav.battle"), Is.EqualTo("BATTLE"));
        }

        [Test]
        public void BalanceSnapshot_IsDeterministicAndContainsAllCatalogRows()
        {
            var first = IdleHeroDefense.Configuration.BalanceSnapshotService.CreateCanonicalText();
            var second = IdleHeroDefense.Configuration.BalanceSnapshotService.CreateCanonicalText();
            Assert.That(second, Is.EqualTo(first));
            foreach (var id in IdleHeroDefense.Configuration.HeroCatalog.AllIds)
                Assert.That(first, Does.Contain($"HERO,{id},"));
            foreach (var product in ProductCatalog.Products)
                Assert.That(first, Does.Contain($"PRODUCT,{product.Id},"));
        }

        [Test]
        public void ObjectPool_ReusesReleasedInstancesAndTracksState()
        {
            var created = 0;
            var pool = new ObjectPool<object>(() => { created++; return new object(); }, initialCapacity: 1);
            var first = pool.Acquire();
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            Assert.That(pool.Release(first), Is.True);
            Assert.That(pool.Release(first), Is.False);
            var reused = pool.Acquire();
            Assert.That(reused, Is.SameAs(first));
            Assert.That(created, Is.EqualTo(1));
        }

        [Test]
        public void Haptics_RespectPreferenceAndReducedMotion()
        {
            var profile = PlayerProfile.CreateNew("player", DateTimeOffset.UtcNow);
            var provider = new CountingHaptics();
            var feedback = new FeedbackService(profile, provider);
            Assert.That(feedback.TryUltimatePulse(), Is.True);
            profile.reducedMotion = true;
            Assert.That(feedback.TryUltimatePulse(), Is.False);
            profile.reducedMotion = false;
            profile.hapticsEnabled = false;
            Assert.That(feedback.TryUltimatePulse(), Is.False);
            Assert.That(provider.Pulses, Is.EqualTo(1));
        }

        [Test]
        public void OfflineBackend_ProfileSyncUsesOptimisticVersion()
        {
            var now = DateTimeOffset.UtcNow;
            var profile = PlayerProfile.CreateNew("player", now);
            profile.GetOrCreateHero("ember_knight");
            var backend = new OfflineGameBackend(profile, new FixedClock { UtcNow = now });
            var session = backend.AuthenticateGuestAsync("install", System.Threading.CancellationToken.None).Result;
            var snapshot = backend.GetProfileAsync(session, System.Threading.CancellationToken.None).Result;
            var updated = backend.UpdateProfileAsync(session, new BackendProfileUpdate
            {
                expectedVersion = snapshot.version, highestStage = 2, activeFormation = new[] { "ember_knight" }
            }, System.Threading.CancellationToken.None).Result;
            Assert.That(updated.version, Is.EqualTo(snapshot.version + 1));
            Assert.That(updated.highestStage, Is.EqualTo(2));
        }

        [Test]
        public void OfflineBackend_SummonRequestReplaysWithoutDuplicateShards()
        {
            var now = DateTimeOffset.UtcNow;
            var profile = PlayerProfile.CreateNew("player", now);
            var backend = new OfflineGameBackend(profile, new FixedClock { UtcNow = now });
            var session = backend.AuthenticateGuestAsync("install", System.Threading.CancellationToken.None).Result;
            var request = new BackendSummonRequest { requestId = "summon-1" };
            var first = backend.SummonAsync(session, request, System.Threading.CancellationToken.None).Result;
            var replay = backend.SummonAsync(session, request, System.Threading.CancellationToken.None).Result;
            Assert.That(first.success, Is.True);
            Assert.That(replay.heroId, Is.EqualTo(first.heroId));
            Assert.That(profile.GetShards(first.heroId), Is.EqualTo(first.shards));
            Assert.That(profile.gems, Is.Zero);
        }

        [Test]
        public void OfflineBackend_ProgressionMutationIsIdempotentAndRejectsCollision()
        {
            var now = DateTimeOffset.UtcNow;
            var profile = PlayerProfile.CreateNew("player", now);
            var backend = new OfflineGameBackend(profile, new FixedClock { UtcNow = now });
            var session = backend.AuthenticateGuestAsync("install", System.Threading.CancellationToken.None).Result;
            var request = new BackendProgressionRequest
            { requestId = "mutation-1", kind = BackendProgressionKind.UpgradeBase, targetId = string.Empty };
            var first = backend.MutateProgressionAsync(session, request, System.Threading.CancellationToken.None).Result;
            var replay = backend.MutateProgressionAsync(session, request, System.Threading.CancellationToken.None).Result;
            var collision = backend.MutateProgressionAsync(session, new BackendProgressionRequest
            { requestId = "mutation-1", kind = BackendProgressionKind.UpgradeArtifact, targetId = "war_banner" },
                System.Threading.CancellationToken.None).Result;
            Assert.That(first.success, Is.True);
            Assert.That(replay.wasReplay, Is.True);
            Assert.That(profile.baseLevel, Is.EqualTo(2));
            Assert.That(profile.gold, Is.EqualTo(300));
            Assert.That(collision.success, Is.False);
            Assert.That(collision.error, Does.Contain("collision"));
        }

        [Test]
        public void OfflineBackend_BattleAndIdleRewardsReplayExactlyOnce()
        {
            var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var clock = new FixedClock { UtcNow = now };
            var profile = PlayerProfile.CreateNew("player", now);
            var backend = new OfflineGameBackend(profile, clock);
            var session = backend.AuthenticateGuestAsync("install", System.Threading.CancellationToken.None).Result;
            var start = backend.StartBattleAsync(session, new BackendBattleStartRequest
                { requestId = "start-1", mode = GameMode.Campaign }, System.Threading.CancellationToken.None).Result;
            var transcript = new CombatTranscript();
            transcript.SetStep(10);
            transcript.Record(10, new DamageEvent("ember_knight", "boss", 1740, false));
            var battle = new BackendBattleRequest { requestId = "battle-1", ticketId = start.ticketId, mode = GameMode.Campaign,
                totalSteps = 10, transcriptHash = transcript.ComputeHash(), events = new[] { new BackendTranscriptEvent
                { step = 10, sourceId = "ember_knight", targetId = "boss", amount = 1740 } } };
            var first = backend.CompleteBattleAsync(session, battle, System.Threading.CancellationToken.None).Result;
            var replay = backend.CompleteBattleAsync(session, battle, System.Threading.CancellationToken.None).Result;
            Assert.That(first.rewardGold, Is.EqualTo(100));
            Assert.That(replay.wasReplay, Is.True);
            Assert.That(profile.gold, Is.EqualTo(600));
            clock.UtcNow = now.AddHours(2);
            var idle = backend.ClaimIdleAsync(session, new BackendIdleRequest { requestId = "idle-1" },
                System.Threading.CancellationToken.None).Result;
            Assert.That(idle.rewardGold, Is.EqualTo(480));
            Assert.That(profile.gold, Is.EqualTo(1080));
        }

        [Test]
        public void CombatTranscript_HashIsDeterministicAndDetectsMutation()
        {
            var first = new CombatTranscript();
            first.SetStep(12);
            first.Record(3, new DamageEvent("knight", "slime", 20, false));
            first.Record(12, new DamageEvent("knight", "boss", 100, true));
            var second = new CombatTranscript();
            second.SetStep(12);
            second.Record(3, new DamageEvent("knight", "slime", 20, false));
            second.Record(12, new DamageEvent("knight", "boss", 100, true));
            Assert.That(first.ComputeHash(), Is.EqualTo(second.ComputeHash()));
            second.Record(12, new DamageEvent("knight", "boss", 1, false));
            Assert.That(first.ComputeHash(), Is.Not.EqualTo(second.ComputeHash()));
        }
    }
}
