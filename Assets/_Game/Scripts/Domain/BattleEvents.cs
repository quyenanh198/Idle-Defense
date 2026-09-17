using System;

namespace IdleHeroDefense.Domain
{
    public enum BattleState { NotStarted, Running, Victory, Defeat }

    public readonly struct DamageEvent
    {
        public readonly string SourceId;
        public readonly string TargetId;
        public readonly int Amount;
        public readonly bool IsUltimate;

        public DamageEvent(string sourceId, string targetId, int amount, bool isUltimate)
        {
            SourceId = sourceId;
            TargetId = targetId;
            Amount = amount;
            IsUltimate = isUltimate;
        }
    }

    public interface IBattleEventSink
    {
        void OnStateChanged(BattleState state);
        void OnDamage(DamageEvent damageEvent);
        void OnEnergyChanged(string heroId, int energy, int maximum);
    }

    public sealed class NullBattleEventSink : IBattleEventSink
    {
        public static readonly NullBattleEventSink Instance = new NullBattleEventSink();
        private NullBattleEventSink() { }
        public void OnStateChanged(BattleState state) { }
        public void OnDamage(DamageEvent damageEvent) { }
        public void OnEnergyChanged(string heroId, int energy, int maximum) { }
    }
}

