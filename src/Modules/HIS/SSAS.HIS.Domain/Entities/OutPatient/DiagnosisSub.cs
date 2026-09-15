using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class DiagnosisSub : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string NameEng { get; set; }
        public string NameArab { get; set; }
        public int MainId { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
