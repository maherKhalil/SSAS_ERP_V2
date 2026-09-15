using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class BedSwap : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public string BedFrom { get; set; }
        public string BedTo { get; set; }
        public string PatientFrom { get; set; }
        public string PatientTo { get; set; }
        public DateTime DateSwap { get; set; }
        public string BedSwapStatus { get; set; }
        public string RejectionReson { get; set; }
        public string PatientFromReadyToSwap { get; set; }
        public string PatientToReadyToSwap { get; set; }
        public string PatientFromSwap { get; set; }
        public string PatientToSwap { get; set; }
        public string SwapReson { get; set; }
        public DateTime ApproveDate { get; set; }
        public int RequesterNurseID { get; set; }
        public int ApprovalNurseID { get; set; }
        public int PatientFromSwapNurseID { get; set; }
        public int PatientToSwapNurseID { get; set; }
        public int PatientFromReadyToSwapNurseID { get; set; }
        public int PatientToReadyToSwapNurseID { get; set; }
        public string UCreateby { get; set; }
        public DateTime PatientFromSwapDate { get; set; }
        public DateTime PatientToSwapDate { get; set; }
        public DateTime PatientFromReadyToSwapDate { get; set; }
        public DateTime PatientToReadyToSwapDate { get; set; }
        public string PatientFrom_EscortBedFrom { get; set; }
        public string PatientFrom_EscortBedTo { get; set; }
        public string PatientTo_EscortBedFrom { get; set; }
        public string PatientTo_EscortBedTo { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
