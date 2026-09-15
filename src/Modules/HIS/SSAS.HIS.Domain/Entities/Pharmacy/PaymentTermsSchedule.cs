using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Pharmacy;

public class PaymentTermsSchedule : Entity<string>, ITenantOwnedEntity
{

    public PaymentTermsSchedule(string id) : base(id) { }
    public PaymentTermsSchedule() : base(Guid.NewGuid().ToString()) { }

    public string? PaymentTermsID { get; set; }
    public string? ScheduleDay { get; set; }
    public string? SchedulePrecentage { get; set; }
    public string? Remarks { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedDate { get; set; }
    public string? LastModifiedBy { get; set; }
    public string? LastModifiedDate { get; set; }
    public string? CompanyID { get; set; }
    public string? TimeFrom { get; set; }
    public Guid TenantId { get; set; }

}
