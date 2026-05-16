using Microsoft.Extensions.Logging;
using Ryla.Core.Ports.Outbound;

namespace Ryla.Core.UseCases;

internal sealed class TestNotificationUseCase(
    IConnectionRepository connectionRepository,
    ILineNotifier lineNotifier,
    ILogger<TestNotificationUseCase> logger) : ITestNotificationUseCase
{
    public async Task<TestNotificationResult> ExecuteAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        var credentials = await connectionRepository.GetLineCredentialsAsync(tenantId, ct);

        if (credentials is null)
        {
            logger.LogWarning("TestNotification: LINE credentials not found for tenant {TenantId}", tenantId);
            return new TestNotificationResult(false, "ไม่พบ LINE connection กรุณาเพิ่ม LINE OA ก่อน");
        }

        var testOrderId = $"TEST-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        var message = $"ทดสอบ Ryla\nร้านค้าเชื่อมต่อสำเร็จ!\nOrder: {testOrderId}";

        var result = await lineNotifier.PushAsync(
            credentials.ChannelAccessToken,
            credentials.TargetUserId,
            message,
            ct);

        if (!result.Success)
        {
            logger.LogWarning(
                "TestNotification: LINE push failed for tenant {TenantId}: {Error}",
                tenantId, result.ErrorMessage);
            return new TestNotificationResult(false, $"ส่ง LINE ไม่สำเร็จ: {result.ErrorMessage}");
        }

        logger.LogInformation("TestNotification: success for tenant {TenantId}", tenantId);
        return new TestNotificationResult(true);
    }
}
