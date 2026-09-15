using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class CancellationOfPatientRequest : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int OrderID { get; set; }
        public int UserID { get; set; }
        public string ReasonCancellation { get; set; }
        public DateTime CancellationDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
