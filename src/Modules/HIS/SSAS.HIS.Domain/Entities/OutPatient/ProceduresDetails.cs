using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class ProceduresDetails : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public string Code { get; set; }
        public string NameArabic { get; set; }
        public string NameEnglish { get; set; }
        public int ProceduresMasterID { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime ModificationDate { get; set; }
        public Guid TenantId { get; set; }
    }
}
