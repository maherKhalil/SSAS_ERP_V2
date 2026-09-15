using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.BloodBank;

public class ClabsiBundle : Entity<string>, ITenantOwnedEntity
{

    public ClabsiBundle(string id) : base(id) { }
    public ClabsiBundle() : base(Guid.NewGuid().ToString()) { }

    public string? CreatedBy { get; set; }
    public string? CreationDate { get; set; }
    public string? ModifiedBy { get; set; }
    public string? ModificationDate { get; set; }
    public string? NurseID { get; set; }
    public string? DoctorID { get; set; }
    public string? PatientId { get; set; }
    public string? Appropriateindicationforindwellingurinarycatheterplacement { get; set; }
    public string? RiskforUTIevaluatedpriortopatientcatheterization { get; set; }
    public string? Alternativestoindwellingcatheterplacementdiscussedpriortocatheterization { get; set; }
    public string? AnymajorpreexistingconditionsuchasDMmalnutritionorrenalinsufficiency { get; set; }
    public string? Performhandhygieneimmediatelybeforeandafterinsertionofthecatheterdeviceorsite { get; set; }
    public string? Securecathetertubingtopreventurethralirritationandmovementofurethraltraction { get; set; }
    public string? Usesterile { get; set; }
    public string? Positionthedrainage { get; set; }
    public string? Maintainstrict { get; set; }
    public string? Checksystem { get; set; }
    public string? Strictprolongedimmobilization { get; set; }
    public string? Bladderoutletobstruction { get; set; }
    public string? Improvecomfortforendoflife { get; set; }
    public string? Assist { get; set; }
    public string? CompanyID { get; set; }
    public string? AppropriateindicationforindwellingurinarycatheterplacementOther { get; set; }
    public Guid TenantId { get; set; }

}
