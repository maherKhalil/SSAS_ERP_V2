using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class PatientOrderMaster : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int PatientId { get; set; }
        public int DoctorId { get; set; }
        public DateTime OrderDate { get; set; }
        public string Period { get; set; }
        public string PeriodType { get; set; }
        public int RepetTypeId { get; set; }
        public int OrderCategId { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public string OrderTarget { get; set; }
        public string OPIPNo { get; set; }
        public bool ISMedicine { get; set; }
        public string Priority { get; set; }
        public bool IsEditable { get; set; }
        public string OrderStatus { get; set; }
        public string Code { get; set; }
        public int FrequencyID { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime StartTime { get; set; }
        public string TransferJustification { get; set; }
        public int TransferUserId { get; set; }
        public bool IsOperation { get; set; }
        public int BranchId { get; set; }
        public Guid TenantId { get; set; }
    }
}
