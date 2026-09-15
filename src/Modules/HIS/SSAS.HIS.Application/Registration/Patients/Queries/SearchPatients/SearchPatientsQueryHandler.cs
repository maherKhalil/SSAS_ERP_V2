using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SSAS.BuildingBlocks.Domain;
using SSAS.HIS.Application.Registration.Patients.Queries.GetPatientById;

namespace SSAS.HIS.Application.Registration.Patients.Queries.SearchPatients;

public sealed class SearchPatientsQueryHandler(IHisReadService readService)
{
    public async Task<Result<List<PatientDto>>> HandleAsync(SearchPatientsQuery query, CancellationToken cancellationToken = default)
    {
        var patients = await readService.SearchPatientsAsync(query.SearchTerm ?? "", cancellationToken);
        return Result.Success(patients);
    }
}
