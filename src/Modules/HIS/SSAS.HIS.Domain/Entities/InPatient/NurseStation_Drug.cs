using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class NurseStation_Drug : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public int drugID { get; set; }
        public string ReceivedQty { get; set; }
        public string DispensedQty { get; set; }
        public string OPIPNo { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public int SourceOrderDetailID { get; set; }
        public int DestinationOrderDetailID { get; set; }
        public string pharmacist { get; set; }
        public string DispensedPharmacy { get; set; }
        public string ExecutionBy { get; set; }
        public Guid TenantId { get; set; }
    }
}
