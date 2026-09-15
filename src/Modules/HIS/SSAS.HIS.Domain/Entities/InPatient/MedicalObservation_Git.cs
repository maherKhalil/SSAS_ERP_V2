using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class MedicalObservation_Git : ITenantOwnedEntity
    {
        public int ID { get; set; }
        public int MedicalObservationID { get; set; }
        public string Esophagus1 { get; set; }
        public string Stomach1 { get; set; }
        public string Colon1 { get; set; }
        public string Conclusion1 { get; set; }
        public string Recommendet1 { get; set; }
        public string Esophagus2 { get; set; }
        public string Stomach2 { get; set; }
        public string Colon2 { get; set; }
        public string Conclusion2 { get; set; }
        public string Recommendet2 { get; set; }
        public string Enteroscopy3 { get; set; }
        public string Radiology3 { get; set; }
        public string Conclusion3 { get; set; }
        public string Recommendet3 { get; set; }
        public string Enteroscopy4 { get; set; }
        public string Sonosrcphagc4 { get; set; }
        public string Conclusion4 { get; set; }
        public string Recommendet4 { get; set; }
        public string Esophagus5 { get; set; }
        public string Stomach5 { get; set; }
        public string Duodenum5 { get; set; }
        public string Conclusion5 { get; set; }
        public string Recommendet5 { get; set; }
        public string Indication1 { get; set; }
        public string Indication2 { get; set; }
        public string Indication3 { get; set; }
        public string Indication4 { get; set; }
        public string Indication5 { get; set; }
        public string PreMedication1 { get; set; }
        public string PreMedication2 { get; set; }
        public string PreMedication3 { get; set; }
        public string PreMedication4 { get; set; }
        public string PreMedication5 { get; set; }
        public string P_R1 { get; set; }
        public string Report1 { get; set; }
        public string Stomach5_Fundus { get; set; }
        public string Stomach5_Body { get; set; }
        public string Stomach5_Pylorous { get; set; }
        public string RFR_Indecation { get; set; }
        public string RFR_Premedication { get; set; }
        public string RFR_siteofablation { get; set; }
        public string RFR_Frequency { get; set; }
        public string RFR_Report { get; set; }
        public string RFR_ReportFile { get; set; }
        public string LbR_Indecation { get; set; }
        public string LbR_Premedication { get; set; }
        public string LbR_txtsiteofbiobsy { get; set; }
        public string LbR_Typeofneedle { get; set; }
        public string LbR_Report { get; set; }
        public string LbR_ReportFile { get; set; }
        public string smallinteitie { get; set; }
        public string duodenoscopy { get; set; }
        public string endooscopy { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
