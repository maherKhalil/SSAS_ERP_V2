using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class ScrapReturnHeader : Entity<string>, ITenantOwnedEntity
{

    public ScrapReturnHeader(string id) : base(id) { }
    public ScrapReturnHeader() : base(Guid.NewGuid().ToString()) { }

    public string? OperationNo { get; set; }
    public string? OperationDate { get; set; }
    public string? SubStockID { get; set; }
    public string? SubStockToID { get; set; }
    public string? Remarks { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? EntryCodes { get; set; }
    public Guid TenantId { get; set; }

}
