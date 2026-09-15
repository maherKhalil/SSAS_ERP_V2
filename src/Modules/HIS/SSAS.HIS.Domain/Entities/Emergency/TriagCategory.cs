using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Emergency;

public class TriagCategory : Entity<string>, ITenantOwnedEntity
{

    public TriagCategory(string id) : base(id) { }
    public TriagCategory() : base(Guid.NewGuid().ToString()) { }

    public string? DescriptionArabic { get; set; }
    public string? DescriptionEnglish { get; set; }
    public string? Color { get; set; }
    public string? WaitingTime { get; set; }
    public string? NameKa { get; set; }
    public Guid TenantId { get; set; }

}
