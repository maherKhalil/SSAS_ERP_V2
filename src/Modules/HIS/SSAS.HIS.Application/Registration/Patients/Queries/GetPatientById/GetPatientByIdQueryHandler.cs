using SSAS.BuildingBlocks.Domain;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SSAS.HIS.Application.Registration.Patients.Queries.GetPatientById;

public sealed class GetPatientByIdQueryHandler(IHisReadService readService)
{
    public async Task<Result<PatientDto>> HandleAsync(GetPatientByIdQuery query, CancellationToken cancellationToken = default)
    {
        var patient = await readService.GetPatientByIdAsync(query.PatientId, cancellationToken);
        if (patient is null)
        {
            return Result.Failure<PatientDto>(new Error("Patient.NotFound", "Patient not found."));
        }

        return Result.Success(patient);
    }
}
