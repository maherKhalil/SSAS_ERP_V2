using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.OutPatient
{
    public class ClinicProcedures : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int ProcedureID { get; set; }
        public int ServiceID { get; set; }
        public string ProcedureSlot { get; set; }
        public string FollowUpNum { get; set; }
        public string FollowUpDuration { get; set; }
        public string ItFollowUp { get; set; }
        public string status { get; set; }
        public int ClinicID { get; set; }
        public string CreatedBy { get; set; }
        public DateTime Creationdate { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime ModificationDate { get; set; }
        public string FollowUpPeriod { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
