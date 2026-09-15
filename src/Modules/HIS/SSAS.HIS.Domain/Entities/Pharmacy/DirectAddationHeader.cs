using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class DirectAddationHeader : Entity<string>, ITenantOwnedEntity
{

    public DirectAddationHeader(string id) : base(id) { }
    public DirectAddationHeader() : base(Guid.NewGuid().ToString()) { }

    public string? OperationNo { get; set; }
    public string? OperationDate { get; set; }
    public string? SubStockID { get; set; }
    public string? Remarks { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? BorrowingID { get; set; }
    public Guid TenantId { get; set; }

}
