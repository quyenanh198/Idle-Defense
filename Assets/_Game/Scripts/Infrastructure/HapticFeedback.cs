using System;
using IdleHeroDefense.Progression;
using UnityEngine;

namespace IdleHeroDefense.Infrastructure
{
    public interface IHapticProvider { void Pulse(); }

    public sealed class UnityHapticProvider : IHapticProvider
    {
        public void Pulse()
        {
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }
    }

    public sealed class FeedbackService
    {
        private readonly PlayerProfile profile;
        private readonly IHapticProvider haptics;
        public FeedbackService(PlayerProfile profile, IHapticProvider haptics)
        { this.profile = profile ?? throw new ArgumentNullException(nameof(profile)); this.haptics = haptics ?? throw new ArgumentNullException(nameof(haptics)); }
        public bool TryUltimatePulse()
        {
            if (!profile.hapticsEnabled || profile.reducedMotion) return false;
            haptics.Pulse();
            return true;
        }
    }
}

