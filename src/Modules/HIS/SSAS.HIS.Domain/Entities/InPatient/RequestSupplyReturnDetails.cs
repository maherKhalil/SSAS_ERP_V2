using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class RequestSupplyReturnDetails : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int RequestSupplyReturnHeaderID { get; set; }
        public int RequestSupplyDetailsID { get; set; }
        public string ReturnedQty { get; set; }
        public int UnitConversionID { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public Guid TenantId { get; set; }
    }
}
