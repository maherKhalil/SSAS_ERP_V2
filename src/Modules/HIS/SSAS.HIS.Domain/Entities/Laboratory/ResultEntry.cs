using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class ResultEntry : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int SectionID { get; set; }
        public string SampleNo { get; set; }
        public int PatientId { get; set; }
        public int ServiceID { get; set; }
        public int ResultId { get; set; }
        public string ObservedValue { get; set; }
        public int TechnicianId { get; set; }
        public int GrowthOptionId { get; set; }
        public string Approved { get; set; }
        public string Remarks { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public string Comments { get; set; }
        public int CompanyID { get; set; }
        public int DeviceID { get; set; }
        public string NewSampleNo { get; set; }
        public int BranchId { get; set; }
        public Guid TenantId { get; set; }
    }
}
