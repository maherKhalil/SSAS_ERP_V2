using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class Discharge_Order : ITenantOwnedEntity
    {
        public int id { get; set; }
        public int AdmitPatientID { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime OrderTime { get; set; }
        public int ReasonID { get; set; }
        public string FileURL { get; set; }
        public string HomeMedicine { get; set; }
        public string PFE { get; set; }
        public string Discharge_Doc { get; set; }
        public int DoctorID { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public string ReAdmit { get; set; }
        public Guid TenantId { get; set; }
    }
}
