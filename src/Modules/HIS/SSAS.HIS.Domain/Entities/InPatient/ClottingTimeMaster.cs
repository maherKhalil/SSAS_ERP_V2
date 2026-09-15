using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class ClottingTimeMaster : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public string PatientIPNumber { get; set; }
        public int AdmitDoctorID { get; set; }
        public Guid TenantId { get; set; }
    }
}
