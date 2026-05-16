namespace Ryla.Core.Domain.Webhooks;

public static class PlatformExtensions
{
    public static string DisplayName(this Platform platform) => platform switch
    {
        Platform.TikTokShop => "TikTok Shop",
        Platform.Shopee => "Shopee",
        _ => platform.ToString()
    };
}
