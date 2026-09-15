using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class ResultEntryDetail : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int ResultEntry_Id { get; set; }
        public string Value { get; set; }
        public DateTime TestDate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyId { get; set; }
        public int InvestigationDetailsId { get; set; }
        public string Remarks { get; set; }
        public string Comments { get; set; }
        public int TestDetailsId { get; set; }
        public string ResultFile { get; set; }
        public int BranchId { get; set; }
        public string ValueP { get; set; }
        public Guid TenantId { get; set; }
    }
}
