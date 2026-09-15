using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class InitialDischarge : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int PatientID { get; set; }
        public int DischargeTypeID { get; set; }
        public DateTime Date { get; set; }
        public string Comment { get; set; }
        public int InitiateDischargeReasonID { get; set; }
        public int InitiateDischargeTypeID { get; set; }
        public string Discharged { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
