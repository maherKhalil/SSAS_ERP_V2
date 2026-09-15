using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class MedicalObservationDepartmentDetails : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string BgImgUrl { get; set; }
        public string SerializedDataObject { get; set; }
        public int MedicalObservationID { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreationDate { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime ModificationDate { get; set; }
        public Guid TenantId { get; set; }
    }
}
