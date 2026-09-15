using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.BuildingBlocks.Domain;
using SSAS.HIS.Domain.Entities.Registration;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SSAS.HIS.Application.Registration.Patients.Commands.RegisterPatient;

public sealed class RegisterPatientCommandHandler(IPatientRepository repository, ICurrentTenant currentTenant)
{
    public async Task<Result<Guid>> HandleAsync(RegisterPatientCommand command, CancellationToken cancellationToken = default)
    {
        var patient = new Patient(Guid.NewGuid())
        {
            TenantId = currentTenant.TenantId ?? throw new InvalidOperationException("Tenant is required."),
            FirstName = command.FirstName,
            LastName = command.LastName,
            PhoneNumber = command.PhoneNumber,
            DateOfBirth = command.DateOfBirth,
            Gender = command.Gender
        };

        repository.Add(patient);
        await repository.SaveChangesAsync(cancellationToken);

        return Result.Success(patient.Id);
    }
}
