using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class PermanentPacemakerImplantationReport : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int PatientID { get; set; }
        public string IPNumber { get; set; }
        public DateTime DateOfImplantation { get; set; }
        public string Ys { get; set; }
        public int DoctorID { get; set; }
        public string IndicationForPermanent { get; set; }
        public string ECGBeforePacemaker { get; set; }
        public string PreMedication { get; set; }
        public string IVAntibiotic { get; set; }
        public string LocalAnaesthesia { get; set; }
        public string VenousAccess { get; set; }
        public string PocketSite { get; set; }
        public string LeadInsertionSite { get; set; }
        public string AtrialLead { get; set; }
        public string VentricualLead { get; set; }
        public string BatteryInsertionSite { get; set; }
        public string BatteryFixation { get; set; }
        public string WoundClosure { get; set; }
        public string Subcutaneous { get; set; }
        public string Skin { get; set; }
        public string LocalAntibiotic { get; set; }
        public string PWave { get; set; }
        public string RWave { get; set; }
        public string VentricularPacingThreshold { get; set; }
        public string ImpedanceAtrialLead { get; set; }
        public string ImpedanceVentricularLead { get; set; }
        public string VLeadManufacturer { get; set; }
        public string VLeadModel { get; set; }
        public string AtrialLeadManufacturer { get; set; }
        public string BatteryDataManufacturer { get; set; }
        public string Complications { get; set; }
        public string Antibiotics { get; set; }
        public string AppointmentsForPacemaker { get; set; }
        public string ArtialPacingThreshold { get; set; }
        public DateTime CreationDate { get; set; }
        public string CreatedBy { get; set; }
        public DateTime ModificationDate { get; set; }
        public string ModifiedBy { get; set; }
        public string AtrialLeadModel { get; set; }
        public string BatteryDataModel { get; set; }
        public string VLeadSerialNo { get; set; }
        public string AtrialLeadSerialNo { get; set; }
        public string BatteryDataSerialNo { get; set; }
        public string VLeadType { get; set; }
        public string AtrialLeadType { get; set; }
        public string BatteryDataType { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
