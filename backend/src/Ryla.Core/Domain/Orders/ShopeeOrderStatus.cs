namespace Ryla.Core.Domain.Orders;

/// <summary>
/// Shopee order status constants จาก Open Platform API
/// </summary>
public static class ShopeeOrderStatus
{
    public const string Unpaid = "UNPAID";
    public const string ReadyToShip = "READY_TO_SHIP";
    public const string Processed = "PROCESSED";
    public const string Shipped = "SHIPPED";
    public const string ToConfirmReceive = "TO_CONFIRM_RECEIVE";
    public const string InCancel = "IN_CANCEL";
    public const string Cancelled = "CANCELLED";
    public const string Completed = "COMPLETED";

    // escrow_* fields ใช้ได้เฉพาะ status นี้ — buyer confirmed receipt
    public static bool IsFinalized(string status) => status == Completed;
}
