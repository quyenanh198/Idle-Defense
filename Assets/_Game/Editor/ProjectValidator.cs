using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleHeroDefense.Configuration;
using IdleHeroDefense.Infrastructure;
using IdleHeroDefense.Monetization;
using UnityEditor;
using UnityEngine;

namespace IdleHeroDefense.Editor
{
    public static class ProjectValidator
    {
        [MenuItem("Idle Hero Defense/Validate Project")]
        public static void ValidateMenu()
        {
            ValidateOrThrow();
            Debug.Log("Idle Hero Defense validation passed.");
        }

        public static void ValidateBatch()
        {
            try { ValidateOrThrow(); }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }

        public static void ValidateOrThrow()
        {
            var errors = new List<string>();
            ValidateLiveConfig(errors);
            ValidateCatalogs(errors);
            if (!ContentTools.SnapshotMatches(out var snapshotError)) errors.Add(snapshotError);
            if (PlayerProfileSchemaVersion() < 1) errors.Add("Player profile schema version must be positive.");
            if (errors.Count > 0) throw new InvalidOperationException("Project validation failed:\n- " + string.Join("\n- ", errors));
        }

        private static void ValidateLiveConfig(ICollection<string> errors)
        {
            var path = Path.Combine(Application.streamingAssetsPath, "live-config.json");
            if (!File.Exists(path)) { errors.Add("StreamingAssets/live-config.json is missing."); return; }
            var original = LiveConfigService.Current;
            if (!LiveConfigService.TryApplyJson(File.ReadAllText(path), out var error)) errors.Add($"Live config: {error}");
            LiveConfigService.Reset();
        }

        private static void ValidateCatalogs(ICollection<string> errors)
        {
            foreach (var id in HeroCatalog.AllIds)
            {
                try
                {
                    var hero = HeroCatalog.Get(id);
                    if (hero.Id != id) errors.Add($"Hero catalog id mismatch for {id}.");
                    if (hero.MaxHealth <= 0 || hero.Attack < 0 || hero.Ultimate == null) errors.Add($"Hero {id} has invalid combat data.");
                }
                catch (Exception exception) { errors.Add($"Hero {id}: {exception.Message}"); }
            }
            var productIds = ProductCatalog.Products.Select(x => x.Id).ToArray();
            if (productIds.Distinct().Count() != productIds.Length) errors.Add("Product ids must be unique.");
            if (ProductCatalog.Products.Any(x => x.Gems < 0)) errors.Add("Product gem grants cannot be negative.");
        }

        private static int PlayerProfileSchemaVersion() => IdleHeroDefense.Progression.PlayerProfile.CurrentSchemaVersion;
    }
}
