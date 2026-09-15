using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Application.InPatient;

public record AdmitPatientCommand(int PatientId, int DoctorId, string BedNo, DateTime AdmissionDate) : IRequest<int>;
public record AssignBedCommand(int BedId, int PatientId) : IRequest<bool>;
public record CreateMedicalObservationCommand(int PatientId, string BloodPressure, string Pulse) : IRequest<int>;

public class InPatientCommandHandlers : 
    IRequestHandler<AdmitPatientCommand, int>,
    IRequestHandler<AssignBedCommand, bool>,
    IRequestHandler<CreateMedicalObservationCommand, int>
{
    public Task<int> Handle(AdmitPatientCommand request, CancellationToken cancellationToken) => Task.FromResult(1);
    public Task<bool> Handle(AssignBedCommand request, CancellationToken cancellationToken) => Task.FromResult(true);
    public Task<int> Handle(CreateMedicalObservationCommand request, CancellationToken cancellationToken) => Task.FromResult(1);
}
