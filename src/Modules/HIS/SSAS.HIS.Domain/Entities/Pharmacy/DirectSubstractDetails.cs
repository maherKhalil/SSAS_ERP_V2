using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class DirectSubstractDetails : Entity<string>, ITenantOwnedEntity
{

    public DirectSubstractDetails(string id) : base(id) { }
    public DirectSubstractDetails() : base(Guid.NewGuid().ToString()) { }

    public string? HeaderID { get; set; }
    public string? DrugID { get; set; }
    public string? QTY { get; set; }
    public string? Price { get; set; }
    public string? Amount { get; set; }
    public string? SubStockBatchID { get; set; }
    public string? UnitConversionFactorId { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public Guid TenantId { get; set; }

}
