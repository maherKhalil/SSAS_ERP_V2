using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class ServicesRequestDetails : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int InvestigationRequestID { get; set; }
        public int PatientID { get; set; }
        public string Type { get; set; }
        public int ServicesID { get; set; }
        public decimal Amount { get; set; }
        public string Comment { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
