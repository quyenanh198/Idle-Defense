using System.Security.Cryptography;

namespace IdleHeroDefense.Api;

public interface IReceiptVerifier { Task<bool> VerifyAsync(ReceiptRequest request, CancellationToken cancellationToken); }

public sealed class SafeReceiptVerifier(IHostEnvironment environment, IConfiguration configuration) : IReceiptVerifier
{
    public Task<bool> VerifyAsync(ReceiptRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var allowTest = environment.IsDevelopment() && configuration.GetValue<bool>("AllowTestReceipts");
        var expected = $"test:{request.TransactionId}:{request.ProductId}";
        return Task.FromResult(allowTest && request.Store == "test" && CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(request.Receipt ?? string.Empty), System.Text.Encoding.UTF8.GetBytes(expected)));
    }
}
