using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Radiology
{
    public class ExamDelivery : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int PatientID { get; set; }
        public string IPNumber { get; set; }
        public string AccessionNo { get; set; }
        public int UserID { get; set; }
        public bool ISReport { get; set; }
        public bool ISCD { get; set; }
        public bool ISFilm { get; set; }
        public string DeliveryTo { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
