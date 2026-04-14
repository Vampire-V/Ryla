using Ryla.Core.Domain;

namespace Ryla.Core.Tests.Domain;

public sealed class TenantTests
{
    [Fact]
    public void Tenant__WhenCreated__ShouldStoreAllProperties()
    {
        var id = Guid.NewGuid();
        var name = "Test Business";
        var createdAt = DateTimeOffset.UtcNow;

        var tenant = new Tenant(id, name, createdAt);

        Assert.Equal(id, tenant.Id);
        Assert.Equal(name, tenant.Name);
        Assert.Equal(createdAt, tenant.CreatedAt);
    }

    [Fact]
    public void Tenant__WhenComparedWithSameValues__ShouldBeEqual()
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var tenant1 = new Tenant(id, "Biz", now);
        var tenant2 = new Tenant(id, "Biz", now);

        Assert.Equal(tenant1, tenant2);
    }
}
