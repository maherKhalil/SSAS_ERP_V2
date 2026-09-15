using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class RequestProceduresDetails : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int RequestProceduresID { get; set; }
        public int ProcedureID { get; set; }
        public decimal Amount { get; set; }
        public string Priority { get; set; }
        public bool IsApproved { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
