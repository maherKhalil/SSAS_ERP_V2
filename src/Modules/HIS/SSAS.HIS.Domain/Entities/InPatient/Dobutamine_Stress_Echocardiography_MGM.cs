using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class Dobutamine_Stress_Echocardiography_MGM : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int Dobutamine_Stress_EchocardiographyID { get; set; }
        public string MGM { get; set; }
        public string Type { get; set; }
        public string Basal { get; set; }
        public int Mid { get; set; }
        public string Apical { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
