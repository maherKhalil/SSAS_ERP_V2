using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Laboratory
{
    public class ResultEntryDetails_Findings : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int EmpID { get; set; }
        public string Findings { get; set; }
        public string Concolusion { get; set; }
        public DateTime FindingDate { get; set; }
        public string Status { get; set; }
        public int ResultEntryID { get; set; }
        public int FinalFindEmpId { get; set; }
        public DateTime StatDate { get; set; }
        public int RevisedById { get; set; }
        public int FinalApprovedById { get; set; }
        public int CompanyID { get; set; }
        public int BranchId { get; set; }
        public Guid TenantId { get; set; }
    }
}
