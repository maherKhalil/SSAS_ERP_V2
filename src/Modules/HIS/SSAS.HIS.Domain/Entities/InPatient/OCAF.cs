using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class OCAF : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public string Patient { get; set; }
        public string PlanType { get; set; }
        public string Bifocal { get; set; }
        public string Vertex { get; set; }
        public string Bifocal1 { get; set; }
        public string Glass { get; set; }
        public string Plastic { get; set; }
        public string None { get; set; }
        public string Multi_coated { get; set; }
        public string Medium { get; set; }
        public string Coating { get; set; }
        public string Varilux { get; set; }
        public string Lenticular { get; set; }
        public string Photosensitive { get; set; }
        public string Light { get; set; }
        public string Vision { get; set; }
        public string hIndex { get; set; }
        public string Aspheric { get; set; }
        public string Dark { get; set; }
        public string Colored { get; set; }
        public string chkBifocal { get; set; }
        public string Thickness { get; set; }
        public string Anti_Scratch { get; set; }
        public string Permanent { get; set; }
        public string Disposable { get; set; }
        public string Frames { get; set; }
        public string SpecifyOfPairs { get; set; }
        public string txtLensesSR { get; set; }
        public string txtFrameSR { get; set; }
        public string txtPhysicianSignature { get; set; }
        public DateTime txtDate { get; set; }
        public string txtNameRelationship { get; set; }
        public string txtSignature { get; set; }
        public DateTime txtDateTime { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }
        public int CompanyID { get; set; }
        public string IPOP { get; set; }
        public Guid TenantId { get; set; }
    }
}
