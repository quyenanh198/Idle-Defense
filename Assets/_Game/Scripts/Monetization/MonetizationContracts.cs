using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace IdleHeroDefense.Monetization
{
    public enum ProductKind { Consumable, NonConsumable }

    public sealed class ProductDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public ProductKind Kind { get; }
        public int Gems { get; }
        public string Entitlement { get; }
        public ProductDefinition(string id, string displayName, ProductKind kind, int gems, string entitlement = "")
        { Id = id; DisplayName = displayName; Kind = kind; Gems = gems; Entitlement = entitlement; }
    }

    public static class ProductCatalog
    {
        public static readonly IReadOnlyList<ProductDefinition> Products = new[]
        {
            new ProductDefinition("gems_500", "Gem Pouch", ProductKind.Consumable, 500),
            new ProductDefinition("gems_1200", "Gem Chest", ProductKind.Consumable, 1200),
            new ProductDefinition("founder_badge", "Founder's Badge", ProductKind.NonConsumable, 300, "founder_badge")
        };

        public static ProductDefinition Get(string id)
        {
            foreach (var product in Products) if (product.Id == id) return product;
            throw new KeyNotFoundException($"Unknown product: {id}");
        }
    }

    [Serializable]
    public sealed class StorePurchase
    {
        public string productId;
        public string transactionId;
        public string receipt;
        public string store;
    }

    [Serializable]
    public sealed class ValidationResult
    {
        public bool valid;
        public string error;
        public string transactionId;
    }

    public interface IStoreAdapter
    {
        bool IsAvailable { get; }
        Task<StorePurchase> PurchaseAsync(string productId, CancellationToken cancellationToken);
        Task<IReadOnlyList<StorePurchase>> RestoreAsync(CancellationToken cancellationToken);
    }

    public interface IReceiptValidator
    {
        Task<ValidationResult> ValidateAsync(StorePurchase purchase, CancellationToken cancellationToken);
    }

    public interface IRewardedAdProvider
    {
        bool IsReady { get; }
        Task<RewardedAdCompletion> ShowAsync(string placement, CancellationToken cancellationToken);
    }

    public sealed class RewardedAdCompletion
    {
        public bool Completed { get; set; }
        public string TransactionId { get; set; } = string.Empty;
    }
}
