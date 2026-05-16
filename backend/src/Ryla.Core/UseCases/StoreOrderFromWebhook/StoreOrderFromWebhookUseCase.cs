using Ryla.Core.Ports.Outbound;

namespace Ryla.Core.UseCases.StoreOrderFromWebhook;

internal sealed class StoreOrderFromWebhookUseCase(IOrderRepository orderRepository)
    : IStoreOrderFromWebhookUseCase
{
    public Task ExecuteAsync(
        Guid tenantId,
        string orderSn,
        string status,
        CancellationToken ct = default)
        => orderRepository.UpsertFromWebhookAsync(tenantId, "shopee", orderSn, status, ct);
}
