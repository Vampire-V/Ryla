using Ryla.Core.Domain;

namespace Ryla.Core.Tests.Domain;

public sealed class UserProfileTests
{
    [Fact]
    public void UserProfile__WhenCreated__ShouldStoreAllProperties()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var fullName = "Somchai";
        var createdAt = DateTimeOffset.UtcNow;

        var profile = new UserProfile(id, tenantId, fullName, createdAt);

        Assert.Equal(id, profile.Id);
        Assert.Equal(tenantId, profile.TenantId);
        Assert.Equal(fullName, profile.FullName);
        Assert.Equal(createdAt, profile.CreatedAt);
    }

    [Fact]
    public void UserProfile__WhenFullNameIsNull__ShouldAllowNull()
    {
        var profile = new UserProfile(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            DateTimeOffset.UtcNow);

        Assert.Null(profile.FullName);
    }
}
