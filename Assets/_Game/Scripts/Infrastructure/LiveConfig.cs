using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleHeroDefense.Infrastructure
{
    [Serializable]
    public sealed class LiveEventConfig
    {
        public string id;
        public string title;
        public string startsUtc;
        public string endsUtc;
        public int mailGold;
        public int mailGems;
    }

    [Serializable]
    public sealed class LiveConfig
    {
        public int schemaVersion = 1;
        public int campaignGold = 100;
        public int dailyDungeonGold = 300;
        public int towerBaseGold = 150;
        public int towerGoldPerFloor = 25;
        public int towerGemInterval = 5;
        public int towerGemReward = 20;
        public int dailyDungeonAttempts = 3;
        public int idleCapHours = 8;
        public List<LiveEventConfig> events = new List<LiveEventConfig>();

        public static LiveConfig Defaults() => new LiveConfig();

        public bool IsValid(out string error)
        {
            if (schemaVersion != 1) { error = "Unsupported live-config schema."; return false; }
            if (campaignGold < 0 || dailyDungeonGold < 0 || towerBaseGold < 0 || towerGoldPerFloor < 0)
            { error = "Rewards cannot be negative."; return false; }
            if (dailyDungeonAttempts < 1 || dailyDungeonAttempts > 20)
            { error = "Daily attempts must be between 1 and 20."; return false; }
            if (towerGemInterval < 1 || towerGemInterval > 100 || towerGemReward < 0)
            { error = "Tower gem settings are invalid."; return false; }
            if (idleCapHours < 1 || idleCapHours > 48)
            { error = "Idle cap must be between 1 and 48 hours."; return false; }
            error = string.Empty;
            return true;
        }
    }

    public static class LiveConfigService
    {
        public static LiveConfig Current { get; private set; } = LiveConfig.Defaults();

        public static bool TryApplyJson(string json, out string error)
        {
            if (string.IsNullOrWhiteSpace(json)) { error = "Config JSON is empty."; return false; }
            try
            {
                var candidate = JsonUtility.FromJson<LiveConfig>(json);
                if (candidate == null || !candidate.IsValid(out error)) return false;
                candidate.events = candidate.events ?? new List<LiveEventConfig>();
                Current = candidate;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public static void Reset() => Current = LiveConfig.Defaults();
    }
}
