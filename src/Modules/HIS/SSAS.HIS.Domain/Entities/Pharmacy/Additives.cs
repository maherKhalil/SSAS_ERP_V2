using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class Additives : Entity<string>, ITenantOwnedEntity
{

    public Additives(string id) : base(id) { }
    public Additives() : base(Guid.NewGuid().ToString()) { }

    public string? GenericId { get; set; }
    public string? DrugId { get; set; }
    public string? TemplateId { get; set; }
    public Guid TenantId { get; set; }

}
