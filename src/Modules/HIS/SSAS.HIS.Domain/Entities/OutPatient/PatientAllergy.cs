using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class PatientAllergy : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int PatientId { get; set; }
        public int AllergyDetailID { get; set; }
        public int TypeId { get; set; }
        public int CategoryId { get; set; }
        public int ReactionId { get; set; }
        public string ReactionType { get; set; }
        public int SeverityId { get; set; }
        public int SourceId { get; set; }
        public string Comment { get; set; }
        public string Createby { get; set; }
        public DateTime CreateDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
