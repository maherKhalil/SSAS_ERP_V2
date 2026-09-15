using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class DrugChart : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int PatientID { get; set; }
        public string IPNO { get; set; }
        public int DrugID { get; set; }
        public string Dosage { get; set; }
        public DateTime DrugChartDateDate { get; set; }
        public DateTime Time { get; set; }
        public int DosageUnitID { get; set; }
        public bool IsChecked { get; set; }
        public int PrescrirtionDetailsId { get; set; }
        public int WardPharmacyDetailsId { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
