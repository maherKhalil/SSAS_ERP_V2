using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class CardiacCatheterization : ITenantOwnedEntity
    {
        public int CardiacId { get; set; }
        public int PatientId { get; set; }
        public string IPNumber { get; set; }
        public DateTime Date { get; set; }
        public string RiskFactors { get; set; }
        public string Operators { get; set; }
        public string Procedures { get; set; }
        public string HemodyNamic { get; set; }
        public string Angiogrephic { get; set; }
        public string LeftMainCoronaryArtery { get; set; }
        public string LeftAnteriorDescending { get; set; }
        public string LeftCircumflexArtery { get; set; }
        public string RightCoronary { get; set; }
        public string LeftVentriculography { get; set; }
        public string Recommendations { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
