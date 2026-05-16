using NSubstitute;
using Ryla.Core.Ports.Outbound;
using Ryla.Core.UseCases.SkuCosts;
using SkuCostEntity = Ryla.Core.Domain.Orders.SkuCost;

namespace Ryla.Core.Tests.UseCases.SkuCostTests;

public sealed class GetSkuCostsUseCaseTests
{
    private readonly ISkuCostRepository _skuCostRepo = Substitute.For<ISkuCostRepository>();
    private readonly IGetSkuCostsUseCase _sut;

    private static readonly Guid TenantId = Guid.NewGuid();
    private const string Platform = "shopee";

    public GetSkuCostsUseCaseTests()
    {
        _sut = new GetSkuCostsUseCase(_skuCostRepo);
    }

    [Fact]
    public async Task ExecuteAsync__WhenSkuCostsExist__ShouldReturnListFromRepository()
    {
        // Arrange
        var expected = new List<SkuCostEntity>
        {
            new(Guid.NewGuid(), TenantId, Platform, "SKU-001", "Product A", 50m),
            new(Guid.NewGuid(), TenantId, Platform, "SKU-002", "Product B", 120m)
        };

        _skuCostRepo.GetByTenantAsync(TenantId, Platform, Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        var result = await _sut.ExecuteAsync(TenantId, Platform);

        // Assert
        Assert.Equal(2, result.Count);
        await _skuCostRepo.Received(1).GetByTenantAsync(TenantId, Platform, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync__WhenNoSkuCostsExist__ShouldReturnEmptyList()
    {
        // Arrange
        _skuCostRepo.GetByTenantAsync(TenantId, Platform, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<SkuCostEntity>());

        // Act
        var result = await _sut.ExecuteAsync(TenantId, Platform);

        // Assert
        Assert.Empty(result);
    }
}
