using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class DrugClassificationPeroid : Entity<string>, ITenantOwnedEntity
{

    public DrugClassificationPeroid(string id) : base(id) { }
    public DrugClassificationPeroid() : base(Guid.NewGuid().ToString()) { }

    public string? DrugClassificationId { get; set; }
    public string? Peroid { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? ModifiedBy { get; set; }
    public string? ModifiedDate { get; set; }
    public Guid TenantId { get; set; }

}
