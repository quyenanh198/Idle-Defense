using System;
using System.Threading;
using IdleHeroDefense.Infrastructure;
using IdleHeroDefense.Monetization;
using IdleHeroDefense.Progression;
using UnityEngine;

namespace IdleHeroDefense.Presentation
{
    public sealed class MonetizationController : MonoBehaviour
    {
        private CancellationTokenSource lifetime;
        private IStoreAdapter store;
        private IReceiptValidator validator;
        private IRewardedAdProvider ads;
        public string Status { get; private set; } = "Store SDK not configured";
        public bool StoreAvailable => store != null && store.IsAvailable;
        public bool AdAvailable => ads != null && ads.IsReady;
        public int RewardedAdsRemaining => ProfileController.Instance == null ? 0 :
            new RewardedAdService(ProfileController.Instance.Profile, new SystemClock()).Remaining;

        private void Awake()
        {
            lifetime = new CancellationTokenSource();
            store = new UnavailableStoreAdapter();
            validator = new RejectingReceiptValidator();
            ads = new UnavailableAdProvider();
        }

        public void Configure(IStoreAdapter storeAdapter, IReceiptValidator receiptValidator, IRewardedAdProvider adProvider)
        {
            store = storeAdapter ?? throw new ArgumentNullException(nameof(storeAdapter));
            validator = receiptValidator ?? throw new ArgumentNullException(nameof(receiptValidator));
            ads = adProvider ?? throw new ArgumentNullException(nameof(adProvider));
            Status = store.IsAvailable ? "Store ready" : "Store unavailable";
        }

        public async void Purchase(string productId)
        {
            if (!StoreAvailable) { Status = "Store SDK not configured"; return; }
            try
            {
                Status = "Purchase in progress…";
                var purchase = await store.PurchaseAsync(productId, lifetime.Token);
                var backendController = FindObjectOfType<BackendController>();
                var result = await new MonetizationService(ProfileController.Instance.Profile, validator,
                        backendController == null || !backendController.IsOnline)
                    .ValidateAndGrantAsync(purchase, lifetime.Token);
                Status = result.Success ? (result.WasReplay ? "Purchase already delivered" : "Purchase delivered") : result.Error;
                if (result.Success)
                {
                    if (backendController != null && backendController.IsOnline) await backendController.RefreshProfileAsync();
                    ProfileController.Instance.Save();
                    AnalyticsService.Track("iap_completed", "product_id", productId, "replay", result.WasReplay);
                }
            }
            catch (Exception exception) { Status = exception.Message; }
        }

        public async void RestorePurchases()
        {
            if (!StoreAvailable) { Status = "Store SDK not configured"; return; }
            try
            {
                var restored = await store.RestoreAsync(lifetime.Token);
                var delivered = 0;
                var backendController = FindObjectOfType<BackendController>();
                var service = new MonetizationService(ProfileController.Instance.Profile, validator,
                    backendController == null || !backendController.IsOnline);
                foreach (var purchase in restored)
                    if ((await service.ValidateAndGrantAsync(purchase, lifetime.Token)).Success) delivered++;
                if (backendController != null && backendController.IsOnline) await backendController.RefreshProfileAsync();
                ProfileController.Instance.Save();
                Status = $"Restored {delivered} purchase(s)";
            }
            catch (Exception exception) { Status = exception.Message; }
        }

        public async void ShowRewardedAd()
        {
            var rewards = new RewardedAdService(ProfileController.Instance.Profile, new SystemClock());
            if (rewards.Remaining <= 0) { Status = "Daily rewarded-ad limit reached"; return; }
            if (!AdAvailable) { Status = "Rewarded ads unavailable"; return; }
            try
            {
                var completed = await ads.ShowAsync("shop_gems", lifetime.Token);
                if (completed == null || !completed.Completed || string.IsNullOrWhiteSpace(completed.TransactionId))
                { Status = "Ad was not completed"; return; }
                var backendController = FindObjectOfType<BackendController>();
                if (backendController == null) { Status = "Backend unavailable"; return; }
                var claim = await backendController.ClaimRewardedAdAsync(completed.TransactionId);
                if (!claim.success) { Status = claim.error; return; }
                Status = $"Reward granted: {RewardedAdService.GemReward} gems";
                AnalyticsService.Track("ad_completed", "placement", "shop_gems", "reward_gems", RewardedAdService.GemReward);
            }
            catch (Exception exception) { Status = exception.Message; }
        }

        private void OnDestroy() { lifetime?.Cancel(); lifetime?.Dispose(); }
    }
}
