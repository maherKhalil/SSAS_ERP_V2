using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class SponsorshipConditionSupplierGroupsItems : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int SponsorID { get; set; }
        public DateTime EffectiveDate { get; set; }
        public DateTime EffectiveToDate { get; set; }
        public decimal standardAmount { get; set; }
        public int SupplierItemID { get; set; }
        public string Discountpercentage { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal NetAmount { get; set; }
        public bool ISApproval { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
