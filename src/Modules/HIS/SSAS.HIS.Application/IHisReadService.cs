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

    Task<IEnumerable<ClinicScheduleDto>> GetClinicSchedulesAsync(int clinicId, CancellationToken cancellationToken = default);
    Task<IEnumerable<AdmittedPatientDto>> GetAdmittedPatientsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<BedDto>> GetBedsAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<MedicalObservationDto>> GetMedicalObservationsAsync(int patientId, CancellationToken cancellationToken = default);
}

public record ClinicScheduleDto(int Id, int ClinicId, int DoctorId, DateTime Date);
public record AdmittedPatientDto(int Id, int PatientId, int DoctorId, DateTime AdmissionDate, string BedNo);
public record BedDto(int Id, string BedNumber, string Description, int BedStatusId);
public record MedicalObservationDto(int Id, int PatientId, string BloodPressure, string Pulse);
