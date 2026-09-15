using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class VaccinationChart : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public string OPNumber { get; set; }
        public int vaccinationID { get; set; }
        public string Doses { get; set; }
        public string DosageNo { get; set; }
        public int DoctorId { get; set; }
        public string year { get; set; }
        public string Jan { get; set; }
        public string Feb { get; set; }
        public string Mar { get; set; }
        public string Apr { get; set; }
        public string May { get; set; }
        public string June { get; set; }
        public string July { get; set; }
        public string Aug { get; set; }
        public string Sep { get; set; }
        public string Oct { get; set; }
        public string Nov { get; set; }
        public string Dec { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
