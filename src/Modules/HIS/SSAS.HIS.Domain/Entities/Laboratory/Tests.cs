using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class Tests : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string TestName { get; set; }
        public string Description { get; set; }
        public int SectionId { get; set; }
        public string Code { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public int ServiceId { get; set; }
        public int SampleTypeId { get; set; }
        public int ContainerTypeId { get; set; }
        public Guid TenantId { get; set; }
    }
}
