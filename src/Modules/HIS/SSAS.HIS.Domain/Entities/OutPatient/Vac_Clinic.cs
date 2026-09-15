using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class Vac_Clinic : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int VacationId { get; set; }
        public int ClinicId { get; set; }
        public Guid TenantId { get; set; }
    }
}
