using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Domain;


namespace SSAS.HIS.Application.Registration.Patients.Queries.GetPatientById;

public sealed class GetPatientByIdQueryHandler(IHisDbContext dbContext)
{
    public async Task<Result<PatientDto>> HandleAsync(GetPatientByIdQuery query, CancellationToken cancellationToken = default)
    {
        var patient = await dbContext.Patients
            .Where(p => p.Id == query.PatientId)
            .Select(p => new PatientDto(
                p.Id,
                p.FirstName,
                p.LastName,
                p.PhoneNumber,
                p.DateOfBirth,
                p.Gender
            ))
            .FirstOrDefaultAsync(cancellationToken);

        if (patient is null)
        {
            return Result.Failure<PatientDto>(new Error("Patient.NotFound", "Patient not found."));
        }

        return Result.Success(patient);
    }
}
