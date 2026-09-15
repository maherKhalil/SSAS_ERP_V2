using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Radiology
{
    public class ResultEntryDetails_Findings : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int ResultEntryDetailsID { get; set; }
        public int DoctorID { get; set; }
        public string Findings { get; set; }
        public string Concolusion { get; set; }
        public DateTime FindingDate { get; set; }
        public string Status { get; set; }
        public int RevisedById { get; set; }
        public int FinalApprovedById { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
