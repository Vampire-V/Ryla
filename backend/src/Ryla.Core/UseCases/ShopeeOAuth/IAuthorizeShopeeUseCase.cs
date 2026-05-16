namespace Ryla.Core.UseCases.ShopeeOAuth;

public interface IAuthorizeShopeeUseCase
{
    /// <summary>
    /// สร้าง Shopee Authorization URL สำหรับ redirect user
    /// </summary>
    Task<string> ExecuteAsync(
        string redirectUrl,
        CancellationToken ct = default);
}
