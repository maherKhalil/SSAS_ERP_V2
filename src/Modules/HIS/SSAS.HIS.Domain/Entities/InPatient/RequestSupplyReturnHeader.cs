using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class RequestSupplyReturnHeader : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int RequestSupplyID { get; set; }
        public string RequestSupplyRetuenCode { get; set; }
        public DateTime ReturnDate { get; set; }
        public string Remarks { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public string EntryCode { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
