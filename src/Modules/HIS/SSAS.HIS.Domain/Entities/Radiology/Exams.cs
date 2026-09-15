using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Radiology
{
    public class Exams : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public string Code { get; set; }
        public string LatinName { get; set; }
        public string LocalName { get; set; }
        public bool ISActive { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int DeviceID { get; set; }
        public string Minutes { get; set; }
        public string InvestegationGroupEnum { get; set; }
        public bool ISpregnant { get; set; }
        public bool ISDiabetic { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
