namespace Ryla.Core.UseCases.ShopeeOAuth;

public interface IHandleShopeeCallbackUseCase
{
    /// <summary>
    /// แลก code → tokens → บันทึกลง Vault → trigger 30-day backfill
    /// </summary>
    Task ExecuteAsync(
        Guid tenantId,
        string code,
        long shopId,
        CancellationToken ct = default);
}
