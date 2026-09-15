using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.InPatient
{
    public class OperationRequest : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int DiagnosisId { get; set; }
        public int ProcedureId { get; set; }
        public int OperationWardId { get; set; }
        public int OperationRoomId { get; set; }
        public DateTime OperationDate { get; set; }
        public string Comment { get; set; }
        public int DepId { get; set; }
        public int PatientId { get; set; }
        public int EmergencyUnitID { get; set; }
        public int CompanyID { get; set; }
        public Guid TenantId { get; set; }
    }
}
