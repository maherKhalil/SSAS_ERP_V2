using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class Dobutamine_Stress_Echocardiography_Comments : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int Dobutamine_Stress_EchocardiographyID { get; set; }
        public string Dose { get; set; }
        public string BP { get; set; }
        public string HR { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
