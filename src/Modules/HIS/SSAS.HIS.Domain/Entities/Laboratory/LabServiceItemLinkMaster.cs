using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class LabServiceItemLinkMaster : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int ServiceId { get; set; }
        public int ItemId { get; set; }
        public string UnitUsed { get; set; }
        public string Wastage { get; set; }
        public decimal UnitCost { get; set; }
        public decimal WastageCost { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
