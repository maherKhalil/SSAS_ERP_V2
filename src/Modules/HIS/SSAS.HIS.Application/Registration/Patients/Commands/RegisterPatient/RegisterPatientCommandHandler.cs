using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Application.Abstractions.Tenancy;
using SSAS.HIS.Domain.Entities.Registration;


namespace SSAS.HIS.Application.Registration.Patients.Commands.RegisterPatient;

public sealed class RegisterPatientCommandHandler(IHisDbContext dbContext, ICurrentTenant currentTenant)
{
    public async Task<Result<Guid>> HandleAsync(RegisterPatientCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = currentTenant.TenantId.Value;
        var patient = new Patient(Guid.NewGuid())
        {
            TenantId = tenantId,
            FirstName = command.FirstName,
            LastName = command.LastName,
            PhoneNumber = command.PhoneNumber,
            DateOfBirth = command.DateOfBirth,
            Gender = command.Gender
        };

        dbContext.Patients.Add(patient);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(patient.Id);
    }
}
