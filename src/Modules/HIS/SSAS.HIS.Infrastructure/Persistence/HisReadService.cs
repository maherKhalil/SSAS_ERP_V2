using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SSAS.HIS.Application;
using SSAS.HIS.Application.Registration.Patients.Queries.GetPatientById;
using SSAS.HIS.Application.Setup.Lookups.Queries.GetDoctorsLookup;
using SSAS.HIS.Application.Setup.Lookups.Queries.GetSpecialtiesLookup;
using SSAS.HIS.Application.Setup.Lookups.Queries.GetClinicsLookup;

namespace SSAS.HIS.Infrastructure.Persistence;

public sealed class HisReadService(IHisDbContext context) : IHisReadService
{
    public async Task<PatientDto?> GetPatientByIdAsync(Guid patientId, CancellationToken cancellationToken = default)
    {
        return await context.Patients
            .AsNoTracking()
            .Where(p => p.Id == patientId)
            .Select(p => new PatientDto(p.Id, p.FirstName, p.LastName, p.PhoneNumber, p.DateOfBirth, p.Gender))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<PatientDto>> SearchPatientsAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var query = context.Patients.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLower();
            query = query.Where(p => p.FirstName.ToLower().Contains(term) || p.LastName.ToLower().Contains(term) || p.PhoneNumber.Contains(term));
        }

        return await query
            .Select(p => new PatientDto(p.Id, p.FirstName, p.LastName, p.PhoneNumber, p.DateOfBirth, p.Gender))
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<DoctorLookupDto>> GetDoctorsLookupAsync(CancellationToken cancellationToken = default)
    {
        return await context.Doctors
            .AsNoTracking()
            .Select(d => new DoctorLookupDto(d.Id, d.Name, d.SpecialtyId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<SpecialtyLookupDto>> GetSpecialtiesLookupAsync(CancellationToken cancellationToken = default)
    {
        return await context.Specialties
            .AsNoTracking()
            .Select(s => new SpecialtyLookupDto(s.Id, s.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<ClinicLookupDto>> GetClinicsLookupAsync(CancellationToken cancellationToken = default)
    {
        return await context.Clinics
            .AsNoTracking()
            .Select(c => new ClinicLookupDto(c.ID, c.Code, c.NameArabic, c.NameEnglish))
            .ToListAsync(cancellationToken);
    }
}
