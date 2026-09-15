using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class CancelAdmission : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string Reason { get; set; }
        public int PatientId { get; set; }
        public DateTime DateCancelation { get; set; }
        public int AdmitPatientsID { get; set; }
        public int ReasonID { get; set; }
        public string Note { get; set; }
        public Guid TenantId { get; set; }
    }
}
