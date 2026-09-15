using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class RequestSuppliesHeader : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public int PatientTypeID { get; set; }
        public string OP_IPNo { get; set; }
        public string RequestNo { get; set; }
        public int DoctorID { get; set; }
        public string RequestStatus { get; set; }
        public int SubStoreID { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public int branchId { get; set; }
        public Guid TenantId { get; set; }
    }
}
