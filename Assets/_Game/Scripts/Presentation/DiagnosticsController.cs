using System;
using System.Threading;
using IdleHeroDefense.Infrastructure;
using UnityEngine;

namespace IdleHeroDefense.Presentation
{
    public sealed class DiagnosticsController : MonoBehaviour
    {
        private const float FlushIntervalSeconds = 15f;
        private BufferedAnalyticsSink analytics;
        private CancellationTokenSource lifetime;
        private float nextFlush;
        private bool handlingLog;

        private void Awake()
        {
            lifetime = new CancellationTokenSource();
            var endpoint = PlayerPrefs.GetString("idle_hero_defense.analytics_url", string.Empty);
            IAnalyticsTransport transport = string.IsNullOrWhiteSpace(endpoint)
                ? (IAnalyticsTransport)new DebugAnalyticsTransport()
                : new HttpAnalyticsTransport(endpoint);
            analytics = new BufferedAnalyticsSink(transport);
            AnalyticsService.Sink = analytics;
            Application.logMessageReceived += OnLog;
            ApplyPerformanceProfile();
            AnalyticsService.Track("session_started", "app_version", Application.version,
                "platform", Application.platform, "memory_mb", SystemInfo.systemMemorySize);
            nextFlush = Time.unscaledTime + FlushIntervalSeconds;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextFlush || analytics.IsFlushing) return;
            nextFlush = Time.unscaledTime + FlushIntervalSeconds;
            Flush();
        }

        private async void Flush()
        {
            if (analytics == null || lifetime == null) return;
            await analytics.FlushAsync(lifetime.Token);
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            AnalyticsService.Track("session_backgrounded", "pending_events", analytics.PendingCount);
            analytics.Persist();
            Flush();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused) AnalyticsService.Track("session_foregrounded");
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (handlingLog || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
            handlingLog = true;
            try
            {
                AnalyticsService.Track("client_error", "type", type, "message", Truncate(condition, 500),
                    "stack", Truncate(stackTrace, 2000));
            }
            finally { handlingLog = false; }
        }

        private static void ApplyPerformanceProfile()
        {
            var constrained = SystemInfo.systemMemorySize > 0 && SystemInfo.systemMemorySize < 3000;
            Application.targetFrameRate = constrained ? 30 : 60;
            QualitySettings.vSyncCount = 0;
            QualitySettings.masterTextureLimit = constrained ? 1 : 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        private static string Truncate(string value, int length) => string.IsNullOrEmpty(value) || value.Length <= length
            ? value ?? string.Empty : value.Substring(0, length);

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            analytics?.Persist();
            lifetime?.Cancel();
            lifetime?.Dispose();
        }
    }
}

