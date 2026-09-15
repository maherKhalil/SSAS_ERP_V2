using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class VitalParametersDeptWise : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int DepartmentID { get; set; }
        public int vitalparameterID { get; set; }
        public bool ISCompulsory { get; set; }
        public string Frequency { get; set; }
        public string FrequencyType { get; set; }
        public string MinValue { get; set; }
        public string MaxValue { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
