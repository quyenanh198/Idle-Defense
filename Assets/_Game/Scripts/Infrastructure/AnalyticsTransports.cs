using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace IdleHeroDefense.Infrastructure
{
    public sealed class DebugAnalyticsTransport : IAnalyticsTransport
    {
        public Task SendAsync(AnalyticsBatch batch, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Debug.Log($"[Analytics batch] {batch.events.Count} event(s)");
            return Task.CompletedTask;
        }
    }

    public sealed class HttpAnalyticsTransport : IAnalyticsTransport
    {
        private readonly string endpoint;
        public HttpAnalyticsTransport(string endpoint) => this.endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));

        public async Task SendAsync(AnalyticsBatch batch, CancellationToken cancellationToken)
        {
            using (var request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(batch)));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 10;
                request.SetRequestHeader("Content-Type", "application/json");
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }
                if (request.result != UnityWebRequest.Result.Success)
                    throw new InvalidOperationException($"Analytics endpoint {request.responseCode}: {request.error}");
            }
        }
    }
}

