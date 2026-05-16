using NSubstitute;
using Ryla.Core.Ports.Outbound;
using Ryla.Core.UseCases.SkuCosts;
using SkuCostEntity = Ryla.Core.Domain.Orders.SkuCost;

namespace Ryla.Core.Tests.UseCases.SkuCostTests;

public sealed class UpsertSkuCostUseCaseTests
{
    private readonly ISkuCostRepository _skuCostRepo = Substitute.For<ISkuCostRepository>();
    private readonly IUpsertSkuCostUseCase _sut;

    private static readonly Guid TenantId = Guid.NewGuid();
    private const string Platform = "shopee";

    public UpsertSkuCostUseCaseTests()
    {
        _sut = new UpsertSkuCostUseCase(_skuCostRepo);
    }

    [Fact]
    public async Task ExecuteAsync__WhenCalled__ShouldDelegateToRepositoryWithCorrectParameters()
    {
        // Arrange
        var expected = new SkuCostEntity(Guid.NewGuid(), TenantId, Platform, "SKU-001", "Product A", 75m);

        _skuCostRepo.UpsertAsync(TenantId, Platform, "SKU-001", "Product A", 75m, Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        var result = await _sut.ExecuteAsync(TenantId, Platform, "SKU-001", "Product A", 75m);

        // Assert
        Assert.Equal(expected, result);
        await _skuCostRepo.Received(1).UpsertAsync(TenantId, Platform, "SKU-001", "Product A", 75m, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync__WhenItemNameIsNull__ShouldPassNullToRepository()
    {
        // Arrange
        var expected = new SkuCostEntity(Guid.NewGuid(), TenantId, Platform, "SKU-002", null, 200m);

        _skuCostRepo.UpsertAsync(TenantId, Platform, "SKU-002", null, 200m, Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        var result = await _sut.ExecuteAsync(TenantId, Platform, "SKU-002", null, 200m);

        // Assert
        Assert.Null(result.ItemName);
        await _skuCostRepo.Received(1).UpsertAsync(TenantId, Platform, "SKU-002", null, 200m, Arg.Any<CancellationToken>());
    }
}
