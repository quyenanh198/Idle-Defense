using System;

namespace IdleHeroDefense.Progression
{
    [Serializable]
    public sealed class BackendSummonReceipt
    {
        public string requestId;
        public string heroId;
        public string rarity;
        public int shards;
        public bool wasPity;
        public int gems;
        public int summonPity;
    }

    [Serializable]
    public sealed class BackendMutationReceipt
    {
        public string requestId;
        public string fingerprint;
        public bool success;
        public string error;
        public int version;
        public int gold;
        public int gems;
        public int gearMaterials;
        public int artifactDust;
        public int baseLevel;
        public int targetLevel;
    }

    [Serializable] public sealed class BackendRewardReceipt { public string requestId; public string json; }
    [Serializable] public sealed class BackendIdleReceipt { public string requestId; public string json; }
    [Serializable] public sealed class BackendQuestReceipt { public string requestId; public string questId; public string json; }
}
