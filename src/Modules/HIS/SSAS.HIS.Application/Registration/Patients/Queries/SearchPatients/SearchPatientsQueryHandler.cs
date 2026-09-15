using Microsoft.EntityFrameworkCore;
using SSAS.BuildingBlocks.Domain;
using SSAS.HIS.Application.Registration.Patients.Queries.GetPatientById;

namespace SSAS.HIS.Application.Registration.Patients.Queries.SearchPatients;

public sealed class SearchPatientsQueryHandler(IHisDbContext dbContext)
{
    public async Task<Result<List<PatientDto>>> HandleAsync(SearchPatientsQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = dbContext.Patients.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.ToLower();
            dbQuery = dbQuery.Where(p => 
                p.FirstName.ToLower().Contains(term) || 
                p.LastName.ToLower().Contains(term) || 
                p.PhoneNumber.Contains(term));
        }

        var patients = await dbQuery
            .Select(p => new PatientDto(
                p.Id,
                p.FirstName,
                p.LastName,
                p.PhoneNumber,
                p.DateOfBirth,
                p.Gender
            ))
            .ToListAsync(cancellationToken);

        return Result.Success(patients);
    }
}
