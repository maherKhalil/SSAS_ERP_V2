using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.Emergency;

public class ABGs : Entity<string>, ITenantOwnedEntity
{

    public ABGs(string id) : base(id) { }
    public ABGs() : base(Guid.NewGuid().ToString()) { }

    public string? PatientID { get; set; }
    public string? DoctorID { get; set; }
    public string? PH { get; set; }
    public string? PCO2 { get; set; }
    public string? PO2 { get; set; }
    public string? SA02 { get; set; }
    public string? HCT { get; set; }
    public string? Hb { get; set; }
    public string? BEecf { get; set; }
    public string? Beb { get; set; }
    public string? SBC { get; set; }
    public string? Hco3 { get; set; }
    public string? Tco2 { get; set; }
    public string? A { get; set; }
    public string? AaDo2 { get; set; }
    public string? aA { get; set; }
    public string? RT { get; set; }
    public string? O2cap { get; set; }
    public string? O2CT { get; set; }
    public string? FO2Hb { get; set; }
    public string? KPlus { get; set; }
    public string? NAPlus { get; set; }
    public string? CI { get; set; }
    public string? GLU { get; set; }
    public string? LAC { get; set; }
    public string? BASE { get; set; }
    public string? Comments { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreationDate { get; set; }
    public string? CompanyID { get; set; }
    public Guid TenantId { get; set; }

}
