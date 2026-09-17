using System;

namespace IdleHeroDefense.Progression
{
    public enum CurrencyType { Gold, Gems }

    public readonly struct EconomyResult
    {
        public readonly bool Success;
        public readonly string Reason;
        public readonly int Balance;

        public EconomyResult(bool success, string reason, int balance)
        {
            Success = success;
            Reason = reason;
            Balance = balance;
        }
    }

    public sealed class EconomyService
    {
        private readonly PlayerProfile profile;
        public EconomyService(PlayerProfile profile) => this.profile = profile ?? throw new ArgumentNullException(nameof(profile));

        public int Balance(CurrencyType type) => type == CurrencyType.Gold ? profile.gold : profile.gems;

        public EconomyResult Grant(CurrencyType type, int amount)
        {
            if (amount <= 0) return new EconomyResult(false, "Amount must be positive.", Balance(type));
            SetBalance(type, checked(Balance(type) + amount));
            return new EconomyResult(true, string.Empty, Balance(type));
        }

        public EconomyResult Spend(CurrencyType type, int amount)
        {
            if (amount <= 0) return new EconomyResult(false, "Amount must be positive.", Balance(type));
            if (Balance(type) < amount) return new EconomyResult(false, "Insufficient currency.", Balance(type));
            SetBalance(type, Balance(type) - amount);
            return new EconomyResult(true, string.Empty, Balance(type));
        }

        private void SetBalance(CurrencyType type, int amount)
        {
            if (type == CurrencyType.Gold) profile.gold = amount;
            else profile.gems = amount;
        }
    }
}

