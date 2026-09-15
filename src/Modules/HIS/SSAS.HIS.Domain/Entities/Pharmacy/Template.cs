using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class Template : Entity<string>, ITenantOwnedEntity
{

    public Template(string id) : base(id) { }
    public Template() : base(Guid.NewGuid().ToString()) { }

    public string? TemplateCode { get; set; }
    public string? TemplateName { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
