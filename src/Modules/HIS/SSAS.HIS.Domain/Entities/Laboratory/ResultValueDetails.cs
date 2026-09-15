using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class ResultValueDetails : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string AgeFrom { get; set; }
        public string AgeTo { get; set; }
        public int SexId { get; set; }
        public string MinimumValue { get; set; }
        public string MaximumValue { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public int ResultValueHeaderID { get; set; }
        public Guid TenantId { get; set; }
    }
}
