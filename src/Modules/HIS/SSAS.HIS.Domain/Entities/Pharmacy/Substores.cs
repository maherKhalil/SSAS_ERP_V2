using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class Substores : Entity<string>, ITenantOwnedEntity
{

    public Substores(string id) : base(id) { }
    public Substores() : base(Guid.NewGuid().ToString()) { }

    public string? SubstoreName { get; set; }
    public string? CostCenterCompaniesID { get; set; }
    public string? AllowSupplierTransactions { get; set; }
    public string? IsDispensingOfDrugs { get; set; }
    public string? IsMainStore { get; set; }
    public string? IsActive { get; set; }
    public string? PatientType { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? MainStockID { get; set; }
    public string? MainAccount_GeneralStock { get; set; }
    public string? SubAccount_GeneralStock { get; set; }
    public string? MainAccount_CostOfGoods { get; set; }
    public string? SubAccount_CostOfGoods { get; set; }
    public string? MainAccount_SalesRevenue { get; set; }
    public string? SubAccount_SalesRevenue { get; set; }
    public string? MainAccount_SettlementByDiscount { get; set; }
    public string? SubAccount_SettlementByDiscount { get; set; }
    public string? MainAccount_SettlementAsWell { get; set; }
    public string? SubAccount_SettlementAsWell { get; set; }
    public string? MainAccount_OutgoingMovements { get; set; }
    public string? SubAccount_OutgoingMovements { get; set; }
    public string? SubstoreCode { get; set; }
    public string? SubstoreNameAr { get; set; }
    public string? Location { get; set; }
    public string? UserCharge { get; set; }
    public string? phone { get; set; }
    public string? WardPharm { get; set; }
    public string? StockType { get; set; }
    public string? IsScrap { get; set; }
    public string? IsDrugStore { get; set; }
    public string? IsProhibitedDrug { get; set; }
    public string? BranchID { get; set; }
    public Guid TenantId { get; set; }

}
