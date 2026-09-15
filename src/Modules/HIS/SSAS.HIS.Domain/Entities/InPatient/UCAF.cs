using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class UCAF : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string Patient { get; set; }
        public string LMP { get; set; }
        public string PlanType { get; set; }
        public string IllnessDuration { get; set; }
        public string Area_Significant { get; set; }
        public string PrincipleCode { get; set; }
        public string SecondCode { get; set; }
        public string HirdCod { get; set; }
        public string ourthCode { get; set; }
        public string Esstimated { get; set; }
        public DateTime dmissionDate { get; set; }
        public string Physician { get; set; }
        public DateTime txtDate { get; set; }
        public string txtRelationship { get; set; }
        public string txtRelationshipSignature { get; set; }
        public DateTime txtRelationshipDate { get; set; }
        public string WorkIN { get; set; }
        public string completed { get; set; }
        public string Chronic { get; set; }
        public string Congenital { get; set; }
        public string RTA { get; set; }
        public string Work { get; set; }
        public string Vaccanation { get; set; }
        public string Checkup { get; set; }
        public string Physicantric { get; set; }
        public string infiritily { get; set; }
        public string pregnancy { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public string reff { get; set; }
        public string IPOP { get; set; }
        public DateTime VisitDate { get; set; }
        public Guid TenantId { get; set; }
    }
}
