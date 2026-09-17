using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace IdleHeroDefense.Infrastructure
{
    public sealed class RemoteConfigController : MonoBehaviour
    {
        private const string CacheKey = "idle_hero_defense.live_config.v1";
        private const string EndpointKey = "idle_hero_defense.live_config.endpoint";

        private IEnumerator Start()
        {
            if (PlayerPrefs.HasKey(CacheKey))
                LiveConfigService.TryApplyJson(PlayerPrefs.GetString(CacheKey), out _);

            var localPath = System.IO.Path.Combine(Application.streamingAssetsPath, "live-config.json");
            var localUri = localPath.Contains("://") ? localPath : new System.Uri(localPath).AbsoluteUri;
            yield return LoadAndApply(localUri, false);

            var endpoint = PlayerPrefs.GetString(EndpointKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(endpoint)) yield return LoadAndApply(endpoint, true);
        }

        private static IEnumerator LoadAndApply(string uri, bool cache)
        {
            using (var request = UnityWebRequest.Get(uri))
            {
                request.timeout = 8;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"Live config unavailable: {request.error}");
                    yield break;
                }
                var json = request.downloadHandler.text;
                if (!LiveConfigService.TryApplyJson(json, out var error))
                {
                    Debug.LogWarning($"Live config rejected: {error}");
                    yield break;
                }
                if (cache)
                {
                    PlayerPrefs.SetString(CacheKey, json);
                    PlayerPrefs.Save();
                }
            }
        }
    }
}
