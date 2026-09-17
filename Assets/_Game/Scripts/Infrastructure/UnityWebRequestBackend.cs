using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace IdleHeroDefense.Infrastructure
{
    public sealed class UnityWebRequestBackend : IGameBackend
    {
        [Serializable] private sealed class GuestRequest { public string installId; }
        private readonly string baseUrl;

        public UnityWebRequestBackend(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl)) throw new ArgumentException("Base URL is required.", nameof(baseUrl));
            this.baseUrl = baseUrl.TrimEnd('/');
        }

        public async Task<BackendSession> AuthenticateGuestAsync(string installId, CancellationToken cancellationToken)
        {
            var json = JsonUtility.ToJson(new GuestRequest { installId = installId });
            return await SendAsync<BackendSession>("/v1/auth/guest", UnityWebRequest.kHttpVerbPOST, json, null, cancellationToken);
        }

        public async Task<EconomyTransactionResponse> ExecuteEconomyAsync(BackendSession session,
            EconomyTransactionRequest request, CancellationToken cancellationToken)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            return await SendAsync<EconomyTransactionResponse>("/v1/economy/transactions", UnityWebRequest.kHttpVerbPOST,
                JsonUtility.ToJson(request), session.accessToken, cancellationToken);
        }

        public Task<BackendProfileSnapshot> GetProfileAsync(BackendSession session, CancellationToken cancellationToken) =>
            SendAsync<BackendProfileSnapshot>("/v1/profile", UnityWebRequest.kHttpVerbGET, null, session.accessToken, cancellationToken);

        public Task<BackendProfileSnapshot> UpdateProfileAsync(BackendSession session, BackendProfileUpdate update,
            CancellationToken cancellationToken) => SendAsync<BackendProfileSnapshot>("/v1/profile", UnityWebRequest.kHttpVerbPUT,
            JsonUtility.ToJson(update), session.accessToken, cancellationToken);

        public Task<BackendSummonResponse> SummonAsync(BackendSession session, BackendSummonRequest request,
            CancellationToken cancellationToken) => SendAsync<BackendSummonResponse>("/v1/summon", UnityWebRequest.kHttpVerbPOST,
            JsonUtility.ToJson(request), session.accessToken, cancellationToken);

        public Task<BackendProgressionResponse> MutateProgressionAsync(BackendSession session,
            BackendProgressionRequest request, CancellationToken cancellationToken) =>
            SendAsync<BackendProgressionResponse>("/v1/progression/mutate", UnityWebRequest.kHttpVerbPOST,
                JsonUtility.ToJson(request), session.accessToken, cancellationToken);

        public Task<BackendRewardResponse> CompleteBattleAsync(BackendSession session, BackendBattleRequest request,
            CancellationToken cancellationToken) => SendAsync<BackendRewardResponse>("/v1/rewards/battle", UnityWebRequest.kHttpVerbPOST,
            JsonUtility.ToJson(request), session.accessToken, cancellationToken);

        public Task<BackendBattleStartResponse> StartBattleAsync(BackendSession session, BackendBattleStartRequest request,
            CancellationToken cancellationToken) => SendAsync<BackendBattleStartResponse>("/v1/battles/start", UnityWebRequest.kHttpVerbPOST,
            JsonUtility.ToJson(request), session.accessToken, cancellationToken);

        public Task<BackendIdleResponse> ClaimIdleAsync(BackendSession session, BackendIdleRequest request,
            CancellationToken cancellationToken) => SendAsync<BackendIdleResponse>("/v1/rewards/idle", UnityWebRequest.kHttpVerbPOST,
            JsonUtility.ToJson(request), session.accessToken, cancellationToken);

        public Task<BackendQuestClaimResponse> ClaimRepeatableQuestAsync(BackendSession session,
            BackendQuestClaimRequest request, CancellationToken cancellationToken) =>
            SendAsync<BackendQuestClaimResponse>("/v1/rewards/quest", UnityWebRequest.kHttpVerbPOST,
                JsonUtility.ToJson(request), session.accessToken, cancellationToken);

        public Task<BackendAdClaimResponse> ClaimRewardedAdAsync(BackendSession session, BackendAdClaimRequest request,
            CancellationToken cancellationToken) => SendAsync<BackendAdClaimResponse>("/v1/ads/claim", UnityWebRequest.kHttpVerbPOST,
            JsonUtility.ToJson(request), session.accessToken, cancellationToken);

        private async Task<T> SendAsync<T>(string path, string method, string json, string bearer,
            CancellationToken cancellationToken) where T : class
        {
            using (var request = new UnityWebRequest(baseUrl + path, method))
            {
                if (!string.IsNullOrEmpty(json)) request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 15;
                request.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrWhiteSpace(bearer)) request.SetRequestHeader("Authorization", $"Bearer {bearer}");
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }
                if (request.result != UnityWebRequest.Result.Success)
                    throw new InvalidOperationException($"Backend {request.responseCode}: {request.error}");
                var result = JsonUtility.FromJson<T>(request.downloadHandler.text);
                if (result == null) throw new InvalidOperationException("Backend returned an empty response.");
                return result;
            }
        }
    }
}
