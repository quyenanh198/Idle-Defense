using System;
using System.Linq;

namespace IdleHeroDefense.Progression
{
    public sealed class MetaProgressionService
    {
        private readonly PlayerProfile profile;
        public MetaProgressionService(PlayerProfile profile) => this.profile = profile ?? throw new ArgumentNullException(nameof(profile));

        public int BaseMaxHealth => 500 + (profile.baseLevel - 1) * 100;
        public int BaseUpgradeCost => profile.baseLevel * 200;
        public float HeroAttackMultiplier => 1f + GetArtifactLevel("war_banner") * 0.03f;
        public float BaseHealthMultiplier => 1f + GetArtifactLevel("guardian_idol") * 0.10f;

        public EconomyResult UpgradeBase()
        {
            var spend = new EconomyService(profile).Spend(CurrencyType.Gold, BaseUpgradeCost);
            if (!spend.Success) return spend;
            profile.baseLevel++;
            return spend;
        }

        public EconomyResult UpgradeArtifact(string artifactId)
        {
            var artifact = GetOrCreateArtifact(artifactId);
            var cost = artifact.level * 50;
            if (profile.artifactDust < cost) return new EconomyResult(false, "Insufficient artifact dust.", profile.artifactDust);
            profile.artifactDust -= cost;
            artifact.level++;
            return new EconomyResult(true, string.Empty, profile.artifactDust);
        }

        public int GetArtifactLevel(string id) => GetOrCreateArtifact(id).level;

        private ArtifactProgress GetOrCreateArtifact(string id)
        {
            var artifact = profile.artifacts.FirstOrDefault(x => x.artifactId == id);
            if (artifact != null) return artifact;
            artifact = new ArtifactProgress(id);
            profile.artifacts.Add(artifact);
            return artifact;
        }
    }
}

