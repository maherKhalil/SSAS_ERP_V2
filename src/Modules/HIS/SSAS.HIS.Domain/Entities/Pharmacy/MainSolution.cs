using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class MainSolution : Entity<string>, ITenantOwnedEntity
{

    public MainSolution(string id) : base(id) { }
    public MainSolution() : base(Guid.NewGuid().ToString()) { }

    public string? GenericId { get; set; }
    public string? DrugId { get; set; }
    public string? TemplateId { get; set; }
    public Guid TenantId { get; set; }

}
