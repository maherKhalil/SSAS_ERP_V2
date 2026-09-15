using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class DCAF : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string Patient { get; set; }
        public string txtEligiberNo { get; set; }
        public string txtDurationOfIllness { get; set; }
        public string txtSignificatSigns { get; set; }
        public string txtDiagnoses { get; set; }
        public string txtPrimary { get; set; }
        public string txtSecondary { get; set; }
        public string txtOtherConditions { get; set; }
        public string txtOther { get; set; }
        public string txtHow { get; set; }
        public string txtWhen { get; set; }
        public string txtWhere { get; set; }
        public string txtCompleted_Coded_By { get; set; }
        public string txtSignature { get; set; }
        public string txtline_of_managment_When_applicable { get; set; }
        public string txtEsstimated { get; set; }
        public DateTime txtAdmissionDate { get; set; }
        public string txtPhysician { get; set; }
        public DateTime txtDate { get; set; }
        public string txtRelationship { get; set; }
        public string txtRelationshipSignature { get; set; }
        public DateTime txtRelationsDate { get; set; }
        public string ChxPlanType { get; set; }
        public string ChxNewVisit { get; set; }
        public string ChxFollowUp { get; set; }
        public string ChxRegularDentalTreatment { get; set; }
        public string ChxDentalCleaning { get; set; }
        public string ChxRTA { get; set; }
        public string ChxWorkRelated { get; set; }
        public string ChxmanagmentY { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public string OPNumber { get; set; }
        public Guid TenantId { get; set; }
    }
}
