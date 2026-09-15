using System;
using SSAS.BuildingBlocks.Domain;
using SSAS.HIS.Domain.Entities.BloodBank;
using SSAS.HIS.Domain.Entities.Emergency;
using SSAS.HIS.Domain.Entities.Pharmacy;
using Xunit;

namespace SSAS.HIS.Domain.Tests;

public class EntityInstantiationTests
{
    [Fact]
    public void BloodBankInventory_ShouldInstantiateAndImplementITenantOwnedEntity()
    {
        var entity = new BloodBankInventory();
        Assert.NotNull(entity.Id);
        Assert.IsAssignableFrom<ITenantOwnedEntity>(entity);
        Assert.IsAssignableFrom<Entity<string>>(entity);
        
        var tenantId = Guid.NewGuid();
        entity.TenantId = tenantId;
        Assert.Equal(tenantId, entity.TenantId);
    }
    
    [Fact]
    public void Emergency_Ambulance_ShouldInstantiateAndImplementITenantOwnedEntity()
    {
        var entity = new EmergencyUnit();
        Assert.NotNull(entity.Id);
        Assert.IsAssignableFrom<ITenantOwnedEntity>(entity);
        
        var tenantId = Guid.NewGuid();
        entity.TenantId = tenantId;
        Assert.Equal(tenantId, entity.TenantId);
    }
    
    [Fact]
    public void Pharmacy_Drug_ShouldInstantiateAndImplementITenantOwnedEntity()
    {
        // Pharmacy has Additives, Drugs? Let's check a table from Pharmacy. We'll use Additives
        var entity = new Additives();
        Assert.NotNull(entity.Id);
        Assert.IsAssignableFrom<ITenantOwnedEntity>(entity);
        
        var tenantId = Guid.NewGuid();
        entity.TenantId = tenantId;
        Assert.Equal(tenantId, entity.TenantId);
    }
}
