using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class HeadUpTiltDiagnosis : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int HeadUpTiltID { get; set; }
        public string ICDCode { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
