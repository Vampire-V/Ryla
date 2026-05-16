using NSubstitute;
using Ryla.Core.Common;
using Ryla.Core.Domain;
using Ryla.Core.Ports.Outbound;
using Ryla.Core.UseCases.TenantResolution;

namespace Ryla.Core.Tests.UseCases;

public sealed class ResolveTenantUseCaseTests
{
    private readonly ITenantRepository _tenantRepo = Substitute.For<ITenantRepository>();
    private readonly ITenantResolver _sut;

    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public ResolveTenantUseCaseTests()
    {
        _sut = new TenantResolverService(_tenantRepo);
    }

    [Fact]
    public async Task ResolveAsync__WhenProfileExists__ShouldReturnTenantId()
    {
        // Arrange
        var profile = new UserProfile(UserId, TenantId, "Test User", DateTimeOffset.UtcNow);
        _tenantRepo.GetProfileByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(profile);

        // Act
        var result = await _sut.ResolveAsync(UserId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(TenantId, result.Value);
    }

    [Fact]
    public async Task ResolveAsync__WhenProfileNotFound__ShouldReturnFailure()
    {
        // Arrange
        _tenantRepo.GetProfileByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns((UserProfile?)null);

        // Act
        var result = await _sut.ResolveAsync(UserId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task ResolveAsync__ShouldCallRepoWithCorrectUserId()
    {
        // Arrange
        var profile = new UserProfile(UserId, TenantId, null, DateTimeOffset.UtcNow);
        _tenantRepo.GetProfileByUserIdAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(profile);

        // Act
        await _sut.ResolveAsync(UserId);

        // Assert
        await _tenantRepo.Received(1).GetProfileByUserIdAsync(UserId, Arg.Any<CancellationToken>());
    }
}
