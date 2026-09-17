using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace IdleHeroDefense.Infrastructure
{
    public interface IAnalyticsSink
    {
        void Track(string eventName, IReadOnlyDictionary<string, object> properties);
    }

    public sealed class DebugAnalyticsSink : IAnalyticsSink
    {
        public void Track(string eventName, IReadOnlyDictionary<string, object> properties)
        {
            var pairs = new List<string>();
            foreach (var pair in properties) pairs.Add($"{pair.Key}={pair.Value}");
            Debug.Log($"[Analytics] {eventName} | {string.Join(", ", pairs)}");
        }
    }

    public static class AnalyticsService
    {
        public static IAnalyticsSink Sink { get; set; } = new DebugAnalyticsSink();
        public static void Track(string eventName, params object[] keyValues)
        {
            if (string.IsNullOrWhiteSpace(eventName)) throw new ArgumentException("Event name is required.", nameof(eventName));
            if (keyValues.Length % 2 != 0) throw new ArgumentException("Analytics properties require key/value pairs.");
            var properties = new Dictionary<string, object>();
            for (var i = 0; i < keyValues.Length; i += 2) properties[Convert.ToString(keyValues[i])] = keyValues[i + 1];
            Sink.Track(eventName, properties);
        }
    }

    [Serializable]
    public sealed class AnalyticsProperty
    {
        public string key;
        public string value;
    }

    [Serializable]
    public sealed class AnalyticsEventData
    {
        public string id;
        public string name;
        public string sessionId;
        public long occurredUtcTicks;
        public List<AnalyticsProperty> properties = new List<AnalyticsProperty>();
    }

    [Serializable]
    public sealed class AnalyticsBatch
    {
        public List<AnalyticsEventData> events = new List<AnalyticsEventData>();
    }

    public interface IAnalyticsTransport
    {
        Task SendAsync(AnalyticsBatch batch, CancellationToken cancellationToken);
    }

    public sealed class BufferedAnalyticsSink : IAnalyticsSink
    {
        private const string QueueKey = "idle_hero_defense.analytics_queue.v1";
        private const int MaximumQueueSize = 200;
        private const int BatchSize = 20;
        private readonly List<AnalyticsEventData> queue = new List<AnalyticsEventData>();
        private readonly IAnalyticsTransport transport;
        private readonly bool persistenceEnabled;
        private readonly string sessionId = Guid.NewGuid().ToString("N");
        public bool IsFlushing { get; private set; }
        public int PendingCount => queue.Count;

        public BufferedAnalyticsSink(IAnalyticsTransport transport, bool loadPersisted = true, bool persistenceEnabled = true)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.persistenceEnabled = persistenceEnabled;
            if (!loadPersisted || !PlayerPrefs.HasKey(QueueKey)) return;
            try
            {
                var snapshot = JsonUtility.FromJson<AnalyticsBatch>(PlayerPrefs.GetString(QueueKey));
                if (snapshot?.events != null) queue.AddRange(snapshot.events);
            }
            catch (Exception exception) { Debug.LogWarning($"Analytics queue recovery failed: {exception.Message}"); }
        }

        public void Track(string eventName, IReadOnlyDictionary<string, object> properties)
        {
            var data = new AnalyticsEventData
            {
                id = Guid.NewGuid().ToString("N"), name = eventName, sessionId = sessionId,
                occurredUtcTicks = DateTimeOffset.UtcNow.UtcDateTime.Ticks
            };
            foreach (var pair in properties)
                data.properties.Add(new AnalyticsProperty { key = pair.Key, value = Convert.ToString(pair.Value, CultureInfo.InvariantCulture) });
            queue.Add(data);
            if (queue.Count > MaximumQueueSize) queue.RemoveRange(0, queue.Count - MaximumQueueSize);
            Persist();
        }

        public async Task<bool> FlushAsync(CancellationToken cancellationToken)
        {
            if (IsFlushing || queue.Count == 0) return false;
            IsFlushing = true;
            try
            {
                var count = Math.Min(BatchSize, queue.Count);
                var batch = new AnalyticsBatch { events = queue.GetRange(0, count) };
                await transport.SendAsync(batch, cancellationToken);
                queue.RemoveRange(0, count);
                Persist();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Analytics flush deferred: {exception.Message}");
                return false;
            }
            finally { IsFlushing = false; }
        }

        public void Persist()
        {
            if (!persistenceEnabled) return;
            PlayerPrefs.SetString(QueueKey, JsonUtility.ToJson(new AnalyticsBatch { events = new List<AnalyticsEventData>(queue) }));
            PlayerPrefs.Save();
        }
    }
}
