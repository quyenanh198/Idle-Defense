using UnityEngine;
using IdleHeroDefense.Infrastructure;

namespace IdleHeroDefense.Presentation
{
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBattleRunner()
        {
            var root = GameObject.Find("IdleHeroDefense") ?? new GameObject("IdleHeroDefense");
            Object.DontDestroyOnLoad(root);
            if (Object.FindObjectOfType<ProfileController>() == null) root.AddComponent<ProfileController>();
            if (Object.FindObjectOfType<BattleRunner>() == null) root.AddComponent<BattleRunner>();
            if (Object.FindObjectOfType<GameShell>() == null) root.AddComponent<GameShell>();
            if (Object.FindObjectOfType<RemoteConfigController>() == null) root.AddComponent<RemoteConfigController>();
            if (Object.FindObjectOfType<BackendController>() == null) root.AddComponent<BackendController>();
            if (Object.FindObjectOfType<MonetizationController>() == null) root.AddComponent<MonetizationController>();
            if (Object.FindObjectOfType<DiagnosticsController>() == null) root.AddComponent<DiagnosticsController>();
            if (Object.FindObjectOfType<LocalizationController>() == null) root.AddComponent<LocalizationController>();
            if (Object.FindObjectOfType<BattlefieldPresenter>() == null) root.AddComponent<BattlefieldPresenter>();
            if (Object.FindObjectOfType<AudioController>() == null) root.AddComponent<AudioController>();
        }
    }
}
