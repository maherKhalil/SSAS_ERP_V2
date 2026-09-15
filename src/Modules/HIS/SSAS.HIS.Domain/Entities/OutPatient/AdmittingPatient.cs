using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class AdmittingPatient : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public string Approx { get; set; }
        public int DoctorsID { get; set; }
        public DateTime AdmitDate { get; set; }
        public DateTime AdmitTime { get; set; }
        public int MLCID { get; set; }
        public string Remarks { get; set; }
        public int WardId { get; set; }
        public int BedNoID { get; set; }
        public bool ISDaycase { get; set; }
        public bool ISEmergency { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public string IPNumber { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
