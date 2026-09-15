using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class TestDetails : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int ComponentId { get; set; }
        public string Desciption { get; set; }
        public int UnitId { get; set; }
        public string AgeFrom { get; set; }
        public string AgeTo { get; set; }
        public string Gender { get; set; }
        public string MinNormalRange { get; set; }
        public string MaxNormalAge { get; set; }
        public string PanicResult { get; set; }
        public int TestId { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string CreatedBy { get; set; }
        public string ModifiedBy { get; set; }
        public string LabTestType { get; set; }
        public bool IsPositive { get; set; }
        public string Range { get; set; }
        public Guid TenantId { get; set; }
    }
}
