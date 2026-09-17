using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleHeroDefense.Domain
{
    public sealed class BattleSimulation
    {
        private readonly List<HeroCombatant> heroes;
        private readonly Queue<IReadOnlyList<EnemyDefinition>> pendingWaves;
        private readonly List<BattleUnit> enemies = new List<BattleUnit>();
        private readonly IBattleEventSink events;
        private readonly List<ActiveStatus> statuses = new List<ActiveStatus>();

        public BattleState State { get; private set; } = BattleState.NotStarted;
        public int BaseHealth { get; private set; }
        public int CurrentWave { get; private set; }
        public IReadOnlyList<HeroCombatant> Heroes => heroes;
        public IReadOnlyList<BattleUnit> Enemies => enemies;
        public CombatStatistics Statistics { get; } = new CombatStatistics();

        private sealed class ActiveStatus
        {
            public string SourceId;
            public BattleUnit Target;
            public StatusEffectDefinition Definition;
            public float Remaining;
            public float TimeToTick;
        }

        public BattleSimulation(IEnumerable<HeroDefinition> heroDefinitions,
            IEnumerable<IReadOnlyList<EnemyDefinition>> waves, int baseHealth,
            IBattleEventSink eventSink = null)
        {
            heroes = heroDefinitions.Select(x => new HeroCombatant(x)).ToList();
            pendingWaves = new Queue<IReadOnlyList<EnemyDefinition>>(waves);
            if (heroes.Count == 0 || heroes.Count > 5) throw new ArgumentException("Formation requires 1-5 heroes.");
            if (pendingWaves.Count == 0) throw new ArgumentException("Battle requires at least one wave.");
            if (baseHealth <= 0) throw new ArgumentOutOfRangeException(nameof(baseHealth));
            BaseHealth = baseHealth;
            events = eventSink ?? NullBattleEventSink.Instance;
        }

        public void Start()
        {
            if (State != BattleState.NotStarted) return;
            State = BattleState.Running;
            SpawnNextWave();
            events.OnStateChanged(State);
        }

        public void Tick(float deltaTime)
        {
            if (State != BattleState.Running || deltaTime <= 0) return;

            TickStatuses(deltaTime);
            if (!enemies.Any(x => x.IsAlive))
            {
                ResolveState();
                if (State != BattleState.Running) return;
            }

            foreach (var hero in heroes.Where(x => x.Unit.IsAlive))
            {
                var target = enemies.FirstOrDefault(x => x.IsAlive);
                if (target == null) break;
                if (!hero.Unit.TickAttack(deltaTime)) continue;
                var damage = target.TakeDamage(hero.Unit.Attack);
                Statistics.RecordDamage(hero.Unit.Id, damage);
                hero.GainAttackEnergy();
                events.OnDamage(new DamageEvent(hero.Unit.Id, target.Id, damage, false));
                events.OnEnergyChanged(hero.Unit.Id, hero.Energy, hero.Definition.UltimateEnergy);
            }

            foreach (var enemy in enemies.Where(x => x.IsAlive))
            {
                if (IsStunned(enemy)) continue;
                if (!enemy.TickAttack(deltaTime)) continue;
                var target = heroes.FirstOrDefault(x => x.Unit.IsAlive);
                if (target != null)
                {
                    var damage = target.Unit.TakeDamage(enemy.Attack);
                    Statistics.RecordDamage(enemy.Id, damage);
                    events.OnDamage(new DamageEvent(enemy.Id, target.Unit.Id, damage, false));
                }
                else
                {
                    BaseHealth = Math.Max(0, BaseHealth - enemy.Attack);
                    Statistics.RecordDamage(enemy.Id, enemy.Attack);
                    events.OnDamage(new DamageEvent(enemy.Id, "base", enemy.Attack, false));
                }
            }

            ResolveState();
        }

        public bool UseUltimate(string heroId)
        {
            if (State != BattleState.Running) return false;
            var hero = heroes.FirstOrDefault(x => x.Unit.Id == heroId);
            if (hero == null || !enemies.Any(x => x.IsAlive) || !hero.ConsumeUltimate()) return false;
            var ability = hero.Definition.Ultimate;
            foreach (var target in SelectTargets(ability.Targeting))
            {
                var damage = target.TakeDamage(ability.Damage);
                Statistics.RecordDamage(heroId, damage);
                events.OnDamage(new DamageEvent(heroId, target.Id, damage, true));
                if (ability.StatusEffect != null && target.IsAlive)
                    statuses.Add(new ActiveStatus
                    {
                        SourceId = heroId,
                        Target = target,
                        Definition = ability.StatusEffect,
                        Remaining = ability.StatusEffect.Duration,
                        TimeToTick = ability.StatusEffect.TickInterval
                    });
            }
            events.OnEnergyChanged(heroId, hero.Energy, hero.Definition.UltimateEnergy);
            ResolveState();
            return true;
        }

        private void ResolveState()
        {
            if (BaseHealth <= 0)
            {
                SetTerminalState(BattleState.Defeat);
                return;
            }

            if (enemies.Any(x => x.IsAlive)) return;
            if (pendingWaves.Count > 0) SpawnNextWave();
            else SetTerminalState(BattleState.Victory);
        }

        private void SpawnNextWave()
        {
            CurrentWave++;
            statuses.Clear();
            enemies.Clear();
            foreach (var definition in pendingWaves.Dequeue())
                enemies.Add(new BattleUnit(definition.Id, definition.MaxHealth, definition.Attack, definition.AttackInterval));
        }

        private IEnumerable<BattleUnit> SelectTargets(TargetingRule rule)
        {
            var living = enemies.Where(x => x.IsAlive);
            if (rule == TargetingRule.AllEnemies) return living.ToList();
            if (rule == TargetingRule.LowestHealthEnemy) return living.OrderBy(x => x.Health).Take(1).ToList();
            return living.Take(1).ToList();
        }

        private bool IsStunned(BattleUnit unit) => statuses.Any(x => x.Target == unit && x.Remaining > 0 &&
            x.Definition.Type == StatusEffectType.Stun);

        private void TickStatuses(float deltaTime)
        {
            for (var i = statuses.Count - 1; i >= 0; i--)
            {
                var status = statuses[i];
                var activeDelta = Math.Min(deltaTime, Math.Max(0, status.Remaining));
                status.Remaining -= deltaTime;
                status.TimeToTick -= activeDelta;
                if (status.Definition.Type == StatusEffectType.Burn)
                {
                    while (status.TimeToTick <= 0 && status.Target.IsAlive)
                    {
                        var damage = status.Target.TakeDamage(status.Definition.DamagePerTick);
                        Statistics.RecordDamage(status.SourceId, damage);
                        events.OnDamage(new DamageEvent(status.SourceId, status.Target.Id, damage, false));
                        status.TimeToTick += status.Definition.TickInterval;
                    }
                }
                if (status.Remaining <= 0 || !status.Target.IsAlive) statuses.RemoveAt(i);
            }
        }

        private void SetTerminalState(BattleState state)
        {
            State = state;
            events.OnStateChanged(state);
        }
    }
}
