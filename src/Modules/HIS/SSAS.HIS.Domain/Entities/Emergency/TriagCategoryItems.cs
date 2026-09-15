using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Emergency;

public class TriagCategoryItems : Entity<string>, ITenantOwnedEntity
{

    public TriagCategoryItems(string id) : base(id) { }
    public TriagCategoryItems() : base(Guid.NewGuid().ToString()) { }

    public string? DescriptionArabic { get; set; }
    public string? DescriptionEnglish { get; set; }
    public string? TriagCategoryID { get; set; }
    public string? NameKa { get; set; }
    public Guid TenantId { get; set; }

}
