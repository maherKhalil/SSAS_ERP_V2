using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class DespatchingDetails : Entity<string>, ITenantOwnedEntity
{

    public DespatchingDetails(string id) : base(id) { }
    public DespatchingDetails() : base(Guid.NewGuid().ToString()) { }

    public string? TransferRequesEntrytId { get; set; }
    public string? SubstoreBatchId { get; set; }
    public string? RecievedQTY { get; set; }
    public string? AcceptedQTY { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
