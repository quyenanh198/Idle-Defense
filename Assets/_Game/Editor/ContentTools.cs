using System;
using System.IO;
using IdleHeroDefense.Configuration;
using UnityEditor;
using UnityEngine;

namespace IdleHeroDefense.Editor
{
    public static class ContentTools
    {
        public const string SnapshotPath = "Docs/balance-snapshot.csv";

        [MenuItem("Idle Hero Defense/Content/Export Balance Snapshot")]
        public static void ExportSnapshot()
        {
            File.WriteAllText(SnapshotPath, BalanceSnapshotService.CreateCanonicalText());
            AssetDatabase.Refresh();
            Debug.Log($"Balance snapshot exported to {SnapshotPath}");
        }

        [MenuItem("Idle Hero Defense/Content/Import & Validate Snapshot")]
        public static void ImportAndValidateSnapshot()
        {
            var source = EditorUtility.OpenFilePanel("Import balance snapshot", string.Empty, "csv");
            if (string.IsNullOrEmpty(source)) return;
            var imported = Normalize(File.ReadAllText(source));
            var canonical = Normalize(BalanceSnapshotService.CreateCanonicalText());
            if (imported != canonical) throw new InvalidOperationException("Imported snapshot does not match the current typed catalogs.");
            File.WriteAllText(SnapshotPath, canonical);
            AssetDatabase.Refresh();
            Debug.Log("Balance snapshot imported and validated.");
        }

        public static bool SnapshotMatches(out string error)
        {
            if (!File.Exists(SnapshotPath)) { error = $"Missing {SnapshotPath}."; return false; }
            if (Normalize(File.ReadAllText(SnapshotPath)) != Normalize(BalanceSnapshotService.CreateCanonicalText()))
            { error = "Balance snapshot is stale. Export and review the diff."; return false; }
            error = string.Empty;
            return true;
        }

        private static string Normalize(string value) => value.Replace("\r\n", "\n").Trim() + "\n";
    }
}

