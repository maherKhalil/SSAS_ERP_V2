using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class ProvisionalDiagnosis : ITenantOwnedEntity
    {
        public int PatientID { get; set; }
        public int AdmitPatientID { get; set; }
        public int ICDCodeID { get; set; }
        public string Type { get; set; }
        public int ID { get; set; }
        public Guid TenantId { get; set; }
    }
}
