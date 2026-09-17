using System.Collections.Generic;

namespace IdleHeroDefense.Domain
{
    public sealed class CombatStatistics
    {
        private readonly Dictionary<string, int> damageBySource = new Dictionary<string, int>();
        public IReadOnlyDictionary<string, int> DamageBySource => damageBySource;

        public void RecordDamage(string sourceId, int amount)
        {
            if (amount <= 0) return;
            damageBySource.TryGetValue(sourceId, out var current);
            damageBySource[sourceId] = current + amount;
        }

        public int DamageFor(string sourceId) => damageBySource.TryGetValue(sourceId, out var amount) ? amount : 0;
    }
}

