using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SSAS.HIS.Application.Registration.Patients.Queries.GetPatientById;
using SSAS.HIS.Application.Setup.Lookups.Queries.GetDoctorsLookup;
using SSAS.HIS.Application.Setup.Lookups.Queries.GetSpecialtiesLookup;
using SSAS.HIS.Application.Setup.Lookups.Queries.GetClinicsLookup;

namespace SSAS.HIS.Application;

public interface IHisReadService
{
    Task<PatientDto?> GetPatientByIdAsync(Guid patientId, CancellationToken cancellationToken = default);
    Task<List<PatientDto>> SearchPatientsAsync(string searchTerm, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<DoctorLookupDto>> GetDoctorsLookupAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<SpecialtyLookupDto>> GetSpecialtiesLookupAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<ClinicLookupDto>> GetClinicsLookupAsync(CancellationToken cancellationToken = default);
}
