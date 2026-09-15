using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class HebaTest : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string PatientType { get; set; }
        public string Name { get; set; }
        public DateTime Birthdate { get; set; }
        public Guid TenantId { get; set; }
    }
}
