using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class ConsultationSetting : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int SpecilaityGroupMasterId { get; set; }
        public bool IsClinicPrice { get; set; }
        public bool IsSpecilaityPrice { get; set; }
        public int ClinicId { get; set; }
        public int ServiceId { get; set; }
        public decimal SpecialtyPrice { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime ModificationDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
