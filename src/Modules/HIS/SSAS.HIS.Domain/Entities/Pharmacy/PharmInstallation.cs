using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class PharmInstallation : Entity<string>, ITenantOwnedEntity
{

    public PharmInstallation(string id) : base(id) { }
    public PharmInstallation() : base(Guid.NewGuid().ToString()) { }

    public string? BarCode { get; set; }
    public string? CreatedBy { get; set; }
    public string? JEwithBatch { get; set; }
    public string? Alert0Qty { get; set; }
    public string? Order0Qty { get; set; }
    public string? IBarCode { get; set; }
    public string? DispenseOPIP { get; set; }
    public string? storeIntegration { get; set; }
    public string? ReturnToDeposit { get; set; }
    public string? UpdatePrice { get; set; }
    public string? SubStoreID { get; set; }
    public string? CreatedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? IsUpdateStock { get; set; }
    public string? IsAddition { get; set; }
    public string? IsDispense { get; set; }
    public string? isReturn { get; set; }
    public string? IsIToD { get; set; }
    public string? IsIToPharmacy { get; set; }
    public string? IstraferStock { get; set; }
    public string? ExpiaryPeriodDays { get; set; }
    public Guid TenantId { get; set; }

}
