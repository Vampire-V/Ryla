using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Ryla.Core.UseCases.SyncOrders;

namespace Ryla.Functions.Functions;

/// <summary>
/// Timer trigger: poll COMPLETED pending orders ทุก 1 ชม.
/// เรียก SyncOrdersUseCase ซึ่ง handle token refresh + escrow data + margin calculation
/// </summary>
internal sealed class SyncOrdersTimerFunction(
    ISyncOrdersUseCase syncOrdersUseCase,
    ILogger<SyncOrdersTimerFunction> logger)
{
    // 50-min budget ต่อ run — function timeout 55 min (host.json)
    private static readonly TimeSpan RunBudget = TimeSpan.FromMinutes(50);

    [Function("SyncOrders")]
    public async Task RunAsync([TimerTrigger("0 0 * * * *")] TimerInfo timerInfo)
    {
        logger.LogInformation(
            "SyncOrders triggered at {Time} (isPastDue={IsPastDue})",
            DateTimeOffset.UtcNow, timerInfo.IsPastDue);

        if (timerInfo.IsPastDue)
            logger.LogWarning("SyncOrders is running late — previous execution may have overrun");

        using var cts = new CancellationTokenSource(RunBudget);

        var result = await syncOrdersUseCase.ExecuteAsync(cts.Token);

        logger.LogInformation(
            "SyncOrders complete: processed={Processed} errors={Errors} authErrors={AuthErrors}",
            result.Processed, result.Errors, result.AuthErrors);

        if (result.AuthErrors > 0)
            logger.LogWarning(
                "{Count} tenant(s) have expired Shopee tokens — sync_status=auth_error set, banner shown in UI",
                result.AuthErrors);
    }
}
