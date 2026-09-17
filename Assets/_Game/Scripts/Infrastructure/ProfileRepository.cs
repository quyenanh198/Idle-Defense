using System;
using IdleHeroDefense.Progression;
using UnityEngine;

namespace IdleHeroDefense.Infrastructure
{
    public interface IProfileRepository
    {
        PlayerProfile LoadOrCreate();
        void Save(PlayerProfile profile);
        void Delete();
    }

    public sealed class PlayerPrefsProfileRepository : IProfileRepository
    {
        private const string ProfileKey = "idle_hero_defense.profile.v1";
        private readonly IClock clock;
        public PlayerPrefsProfileRepository(IClock clock) => this.clock = clock;

        public PlayerProfile LoadOrCreate()
        {
            if (!PlayerPrefs.HasKey(ProfileKey)) return PlayerProfile.CreateNew(Guid.NewGuid().ToString("N"), clock.UtcNow);
            try
            {
                var profile = JsonUtility.FromJson<PlayerProfile>(PlayerPrefs.GetString(ProfileKey));
                if (profile == null || profile.schemaVersion != PlayerProfile.CurrentSchemaVersion)
                    throw new InvalidOperationException("Unsupported profile schema.");
                profile.Normalize();
                return profile;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Profile recovery created a new save: {exception.Message}");
                return PlayerProfile.CreateNew(Guid.NewGuid().ToString("N"), clock.UtcNow);
            }
        }

        public void Save(PlayerProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            PlayerPrefs.SetString(ProfileKey, JsonUtility.ToJson(profile));
            PlayerPrefs.Save();
        }

        public void Delete()
        {
            PlayerPrefs.DeleteKey(ProfileKey);
            PlayerPrefs.Save();
        }
    }
}
