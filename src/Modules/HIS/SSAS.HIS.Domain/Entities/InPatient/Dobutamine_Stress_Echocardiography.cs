using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class Dobutamine_Stress_Echocardiography : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PID { get; set; }
        public DateTime DateofExam { get; set; }
        public string InterPretations { get; set; }
        public string conclustion { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public DateTime TimeofExam { get; set; }
        public int CompanyID { get; set; }
        public string comment { get; set; }
        public Guid TenantId { get; set; }
    }
}
