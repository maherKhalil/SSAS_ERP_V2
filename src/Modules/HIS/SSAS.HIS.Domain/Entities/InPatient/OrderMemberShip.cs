using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class OrderMemberShip : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int UserID { get; set; }
        public int OrderTypeId { get; set; }
        public string VW_Order_vertified { get; set; }
        public string VW_Order_Unvertified { get; set; }
        public string VW_Order_Completed { get; set; }
        public string VW_Order_Resulted { get; set; }
        public string VW_Order_Confirmed { get; set; }
        public string Exc_Order_vertified { get; set; }
        public string Exc_Order_Unvertified { get; set; }
        public string Exc_Order_Completed { get; set; }
        public string Exc_Order_Resulted { get; set; }
        public string Exc_Order_Confirmed { get; set; }
        public string Cncl_Order_vertified { get; set; }
        public string Cncl_Order_Unvertified { get; set; }
        public string Cncl_Order_Completed { get; set; }
        public string Cncl_Order_Resulted { get; set; }
        public string Cncl_Order_Confirmed { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
