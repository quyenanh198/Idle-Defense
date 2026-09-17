using IdleHeroDefense.Infrastructure;
using UnityEngine;

namespace IdleHeroDefense.Presentation
{
    public sealed class LocalizationController : MonoBehaviour
    {
        private void Awake()
        {
            var language = ProfileController.Instance?.Profile.language ?? "en";
            if (!LocalizationService.SetLanguage(language)) LocalizationService.SetLanguage("en");
        }

        public void SetLanguage(string language)
        {
            if (!LocalizationService.SetLanguage(language)) return;
            if (ProfileController.Instance == null) return;
            ProfileController.Instance.Profile.language = LocalizationService.Language;
            ProfileController.Instance.Save();
            IdleHeroDefense.Infrastructure.AnalyticsService.Track("language_changed", "language", LocalizationService.Language);
        }
    }
}

