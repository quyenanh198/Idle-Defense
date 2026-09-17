using System;

namespace IdleHeroDefense.Domain
{
    public sealed class HeroCombatant
    {
        private const int EnergyPerBasicAttack = 20;
        public HeroDefinition Definition { get; }
        public BattleUnit Unit { get; }
        public int Energy { get; private set; }
        public bool CanUseUltimate => Unit.IsAlive && Energy >= Definition.UltimateEnergy;

        public HeroCombatant(HeroDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Unit = new BattleUnit(definition.Id, definition.MaxHealth, definition.Attack, definition.AttackInterval);
        }

        public void GainAttackEnergy()
        {
            Energy = Math.Min(Definition.UltimateEnergy, Energy + EnergyPerBasicAttack);
        }

        public bool ConsumeUltimate()
        {
            if (!CanUseUltimate) return false;
            Energy = 0;
            return true;
        }

        public void SetEnergyForTesting(int amount)
        {
            Energy = Math.Min(Definition.UltimateEnergy, Math.Max(0, amount));
        }
    }
}

