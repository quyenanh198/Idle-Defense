using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace IdleHeroDefense.Monetization
{
    public sealed class UnityWebRequestReceiptValidator : IReceiptValidator
    {
        private readonly string baseUrl;
        private readonly Func<string> accessToken;
        public UnityWebRequestReceiptValidator(string baseUrl, Func<string> accessToken)
        { this.baseUrl = baseUrl?.TrimEnd('/') ?? throw new ArgumentNullException(nameof(baseUrl)); this.accessToken = accessToken ?? throw new ArgumentNullException(nameof(accessToken)); }

        public async Task<ValidationResult> ValidateAsync(StorePurchase purchase, CancellationToken cancellationToken)
        {
            using (var request = new UnityWebRequest(baseUrl + "/v1/iap/validate", UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(purchase)));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = 15;
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", $"Bearer {accessToken()}");
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await Task.Yield();
                }
                if (request.result != UnityWebRequest.Result.Success)
                    return new ValidationResult { valid = false, error = $"Validation service {request.responseCode}: {request.error}" };
                return JsonUtility.FromJson<ValidationResult>(request.downloadHandler.text) ??
                       new ValidationResult { valid = false, error = "Empty validation response." };
            }
        }
    }

    public sealed class RejectingReceiptValidator : IReceiptValidator
    {
        public Task<ValidationResult> ValidateAsync(StorePurchase purchase, CancellationToken cancellationToken) =>
            Task.FromResult(new ValidationResult { valid = false, error = "Receipt validator is not configured." });
    }
}

