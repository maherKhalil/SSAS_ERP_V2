using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class SamplesDispatchedHeader : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string DispatchNO { get; set; }
        public DateTime Date { get; set; }
        public int ExternalAgencyId { get; set; }
        public string EnteredBy { get; set; }
        public string Remarks { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
