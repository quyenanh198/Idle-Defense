using System;
using System.Collections.Generic;
using UnityEngine;

namespace IdleHeroDefense.Infrastructure
{
    [Serializable] public sealed class LocalizationEntry { public string key; public string value; }
    [Serializable] public sealed class LocalizationTable { public string language; public List<LocalizationEntry> entries = new List<LocalizationEntry>(); }

    public static class LocalizationService
    {
        private static readonly Dictionary<string, string> Strings = new Dictionary<string, string>();
        public static string Language { get; private set; } = "en";
        public static event Action Changed;

        public static bool SetLanguage(string language)
        {
            if (string.IsNullOrWhiteSpace(language)) language = "en";
            var asset = Resources.Load<TextAsset>($"Localization/{language}") ?? Resources.Load<TextAsset>("Localization/en");
            if (asset == null) return false;
            var table = JsonUtility.FromJson<LocalizationTable>(asset.text);
            if (table?.entries == null) return false;
            Strings.Clear();
            foreach (var entry in table.entries)
                if (!string.IsNullOrWhiteSpace(entry.key)) Strings[entry.key] = entry.value ?? string.Empty;
            Language = table.language;
            Changed?.Invoke();
            return true;
        }

        public static string Get(string key, string fallback = null)
        {
            if (Strings.Count == 0) SetLanguage(Language);
            return Strings.TryGetValue(key, out var value) ? value : fallback ?? $"[{key}]";
        }

        public static string Format(string key, string fallback, params object[] args) =>
            string.Format(Get(key, fallback), args);
    }
}

