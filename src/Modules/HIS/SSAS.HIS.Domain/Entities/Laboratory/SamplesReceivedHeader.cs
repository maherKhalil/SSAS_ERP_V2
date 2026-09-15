using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class SamplesReceivedHeader : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int ExternalAgencyId { get; set; }
        public string LabNO { get; set; }
        public DateTime Date { get; set; }
        public string ReferanceNO { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public int PatientID { get; set; }
        public int BranchId { get; set; }
        public Guid TenantId { get; set; }
    }
}
