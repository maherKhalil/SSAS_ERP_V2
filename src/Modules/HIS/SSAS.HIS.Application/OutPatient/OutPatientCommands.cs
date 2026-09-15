using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Application.OutPatient;

public record CreateClinicScheduleCommand(int ClinicId, int DoctorId, DateTime StartTime) : IRequest<int>;

public class CreateClinicScheduleCommandHandler : IRequestHandler<CreateClinicScheduleCommand, int>
{
    public Task<int> Handle(CreateClinicScheduleCommand request, CancellationToken cancellationToken)
    {
        return Task.FromResult(1);
    }
}
