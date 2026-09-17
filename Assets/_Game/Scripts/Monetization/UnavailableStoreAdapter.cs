using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace IdleHeroDefense.Monetization
{
    public sealed class UnavailableStoreAdapter : IStoreAdapter
    {
        public bool IsAvailable => false;
        public Task<StorePurchase> PurchaseAsync(string productId, CancellationToken cancellationToken) =>
            Task.FromException<StorePurchase>(new InvalidOperationException("Store SDK is not configured."));
        public Task<IReadOnlyList<StorePurchase>> RestoreAsync(CancellationToken cancellationToken) =>
            Task.FromResult((IReadOnlyList<StorePurchase>)new StorePurchase[0]);
    }

    public sealed class UnavailableAdProvider : IRewardedAdProvider
    {
        public bool IsReady => false;
        public Task<RewardedAdCompletion> ShowAsync(string placement, CancellationToken cancellationToken) =>
            Task.FromResult(new RewardedAdCompletion { Completed = false });
    }
}
