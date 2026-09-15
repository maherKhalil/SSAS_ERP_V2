using System;
using SSAS.BuildingBlocks.Domain;
using SSAS.HIS.Domain.Entities.Radiology;
using SSAS.HIS.Domain.Entities.Laboratory;
using SSAS.HIS.Domain.Entities.InPatient;
using SSAS.HIS.Domain.Entities.OutPatient;
using Xunit;

namespace SSAS.HIS.Domain.Tests
{
    public class EntitiesTests
    {
        [Fact]
        public void RadiologyEntity_CanBeInstantiated_AndImplementsITenantOwnedEntity()
        {
            var entity = new SSAS.HIS.Domain.Entities.Radiology.Tests();
            Assert.IsAssignableFrom<ITenantOwnedEntity>(entity);
            
            var id = Guid.NewGuid();
            entity.TenantId = id;
            Assert.Equal(id, entity.TenantId);
        }

        [Fact]
        public void LaboratoryEntity_CanBeInstantiated_AndImplementsITenantOwnedEntity()
        {
            var entity = new LaboratorySetting();
            Assert.IsAssignableFrom<ITenantOwnedEntity>(entity);
            
            var id = Guid.NewGuid();
            entity.TenantId = id;
            Assert.Equal(id, entity.TenantId);
        }

        [Fact]
        public void InPatientEntity_CanBeInstantiated_AndImplementsITenantOwnedEntity()
        {
            var entity = new Bed();
            Assert.IsAssignableFrom<ITenantOwnedEntity>(entity);
            
            var id = Guid.NewGuid();
            entity.TenantId = id;
            Assert.Equal(id, entity.TenantId);
        }

        [Fact]
        public void OutPatientEntity_CanBeInstantiated_AndImplementsITenantOwnedEntity()
        {
            var entity = new PatientVitals();
            Assert.IsAssignableFrom<ITenantOwnedEntity>(entity);
            
            var id = Guid.NewGuid();
            entity.TenantId = id;
            Assert.Equal(id, entity.TenantId);
        }
    }
}
