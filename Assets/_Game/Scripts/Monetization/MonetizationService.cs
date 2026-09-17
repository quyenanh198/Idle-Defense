using System;
using System.Threading;
using System.Threading.Tasks;
using IdleHeroDefense.Progression;

namespace IdleHeroDefense.Monetization
{
    public sealed class MonetizationResult
    {
        public bool Success { get; }
        public string Error { get; }
        public bool WasReplay { get; }
        public MonetizationResult(bool success, string error = "", bool wasReplay = false)
        { Success = success; Error = error; WasReplay = wasReplay; }
    }

    public sealed class MonetizationService
    {
        private readonly PlayerProfile profile;
        private readonly IReceiptValidator validator;
        private readonly bool grantLocally;
        public MonetizationService(PlayerProfile profile, IReceiptValidator validator, bool grantLocally = true)
        { this.profile = profile ?? throw new ArgumentNullException(nameof(profile)); this.validator = validator ?? throw new ArgumentNullException(nameof(validator)); this.grantLocally = grantLocally; }

        public async Task<MonetizationResult> ValidateAndGrantAsync(StorePurchase purchase, CancellationToken cancellationToken)
        {
            if (purchase == null || string.IsNullOrWhiteSpace(purchase.productId) || string.IsNullOrWhiteSpace(purchase.transactionId))
                return new MonetizationResult(false, "Purchase data is incomplete.");
            if (profile.processedPurchaseIds.Contains(purchase.transactionId)) return new MonetizationResult(true, wasReplay: true);
            ProductDefinition product;
            try { product = ProductCatalog.Get(purchase.productId); }
            catch (Exception exception) { return new MonetizationResult(false, exception.Message); }
            var validation = await validator.ValidateAsync(purchase, cancellationToken);
            if (validation == null || !validation.valid) return new MonetizationResult(false, validation?.error ?? "Receipt validation failed.");
            if (validation.transactionId != purchase.transactionId) return new MonetizationResult(false, "Validated transaction id does not match.");

            if (grantLocally)
            {
                if (product.Gems > 0) new EconomyService(profile).Grant(CurrencyType.Gems, product.Gems);
                if (!string.IsNullOrEmpty(product.Entitlement) && !profile.entitlements.Contains(product.Entitlement))
                    profile.entitlements.Add(product.Entitlement);
            }
            profile.processedPurchaseIds.Add(purchase.transactionId);
            return new MonetizationResult(true);
        }
    }

    public sealed class RewardedAdService
    {
        public const int DailyLimit = 5;
        public const int GemReward = 10;
        private readonly PlayerProfile profile;
        private readonly IClock clock;

        public RewardedAdService(PlayerProfile profile, IClock clock)
        { this.profile = profile ?? throw new ArgumentNullException(nameof(profile)); this.clock = clock ?? throw new ArgumentNullException(nameof(clock)); }

        public int Remaining
        {
            get { RefreshDay(); return Math.Max(0, DailyLimit - profile.rewardedAdsWatched); }
        }

        public bool GrantCompletedView()
        {
            RefreshDay();
            if (profile.rewardedAdsWatched >= DailyLimit) return false;
            profile.rewardedAdsWatched++;
            new EconomyService(profile).Grant(CurrencyType.Gems, GemReward);
            return true;
        }

        private void RefreshDay()
        {
            var date = clock.UtcNow.ToString("yyyy-MM-dd");
            if (profile.rewardedAdUtcDate == date) return;
            profile.rewardedAdUtcDate = date;
            profile.rewardedAdsWatched = 0;
        }
    }
}
