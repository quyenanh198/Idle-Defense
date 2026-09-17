using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace IdleHeroDefense.Domain
{
    [Serializable]
    public sealed class CombatTranscriptEvent
    {
        public int step;
        public string sourceId;
        public string targetId;
        public int amount;
        public bool isUltimate;
    }

    public sealed class CombatTranscript
    {
        private readonly List<CombatTranscriptEvent> events = new List<CombatTranscriptEvent>();
        public IReadOnlyList<CombatTranscriptEvent> Events => events;
        public int TotalSteps { get; private set; }
        public void SetStep(int step) => TotalSteps = Math.Max(TotalSteps, step);
        public void Record(int step, DamageEvent damage)
        {
            events.Add(new CombatTranscriptEvent
            { step = step, sourceId = damage.SourceId, targetId = damage.TargetId, amount = damage.Amount, isUltimate = damage.IsUltimate });
        }
        public string ComputeHash()
        {
            var canonical = new StringBuilder();
            foreach (var item in events)
                canonical.Append(item.step).Append('|').Append(item.sourceId).Append('|').Append(item.targetId).Append('|')
                    .Append(item.amount).Append('|').Append(item.isUltimate ? 1 : 0).Append('\n');
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()))).Replace("-", string.Empty);
        }
    }
}
