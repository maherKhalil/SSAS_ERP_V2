using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class CoronaryIntervention : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientId { get; set; }
        public string IPNumber { get; set; }
        public string RiskFactors { get; set; }
        public string Operators { get; set; }
        public string Procedures { get; set; }
        public string LAD { get; set; }
        public string RCA { get; set; }
        public string Equipments { get; set; }
        public string Comments { get; set; }
        public DateTime CreationDate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime ModificationDate { get; set; }
        public string ModifiedBy { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
