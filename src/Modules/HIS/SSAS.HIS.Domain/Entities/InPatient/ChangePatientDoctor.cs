using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class ChangePatientDoctor : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public int OldDoctorId { get; set; }
        public int NewDoctorId { get; set; }
        public string ChangeReason { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
