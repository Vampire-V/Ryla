namespace Ryla.Core.UseCases.ShopeeOAuth;

public interface IRefreshShopeeTokenUseCase
{
    /// <summary>
    /// Refresh access_token ถ้า expiry < threshold
    /// คืน true ถ้า refresh สำเร็จหรือไม่จำเป็นต้อง refresh
    /// คืน false ถ้า refresh_token หมดอายุ (ต้อง re-OAuth)
    /// </summary>
    Task<RefreshResult> ExecuteAsync(
        Guid tenantId,
        CancellationToken ct = default);
}

public enum RefreshResult
{
    NotRequired,
    Refreshed,
    AuthExpired
}
