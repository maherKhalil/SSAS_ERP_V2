using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class SamplesDispatchedDetails : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int SamplesDispatchedId { get; set; }
        public DateTime Date { get; set; }
        public DateTime Time { get; set; }
        public string SampleNo { get; set; }
        public int TestId { get; set; }
        public int PatientId { get; set; }
        public string Remarks { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public int InvestigationDetailID { get; set; }
        public Guid TenantId { get; set; }
    }
}
