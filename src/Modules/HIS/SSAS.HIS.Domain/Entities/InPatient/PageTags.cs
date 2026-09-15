using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class PageTags : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string PageTagsGroupEnumValue { get; set; }
        public int TagId { get; set; }
        public string PageEnumValue { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
