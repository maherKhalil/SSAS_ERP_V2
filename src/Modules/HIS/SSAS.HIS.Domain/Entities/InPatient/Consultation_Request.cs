using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class Consultation_Request : ITenantOwnedEntity
    {
        public int id { get; set; }
        public int PatientID { get; set; }
        public string Urgency { get; set; }
        public string Request_type { get; set; }
        public string FromDr { get; set; }
        public string ToDr { get; set; }
        public string RequestReason { get; set; }
        public DateTime RequestDate { get; set; }
        public string Request_Response { get; set; }
        public string Response_comment { get; set; }
        public string IPOPNo { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public DateTime RequestTime { get; set; }
        public string OtherType { get; set; }
        public string TransferBedNo { get; set; }
        public string MainReqType { get; set; }
        public string FromNurseID_Ready { get; set; }
        public string ToNurseID_Ready { get; set; }
        public string FromNurseID_Left { get; set; }
        public string ToNurseID_Arrived { get; set; }
        public DateTime FromReady_Date { get; set; }
        public DateTime ToReady_Date { get; set; }
        public DateTime FromLeft_Date { get; set; }
        public DateTime ToArrive_Date { get; set; }
        public string NewEscortBedNo { get; set; }
        public string HandoverStatus { get; set; }
        public string CancelRequest { get; set; }
        public Guid TenantId { get; set; }
    }
}
