using Microsoft.EntityFrameworkCore;
using SSAS.HIS.Domain.Entities.Registration;
using SSAS.HIS.Domain.Entities.OutPatient;
using System.Threading;
using System.Threading.Tasks;

namespace SSAS.HIS.Infrastructure.Persistence;

public interface IHisDbContext
{
    DbSet<Patient> Patients { get; }
    DbSet<Doctor> Doctors { get; }
    DbSet<Specialty> Specialties { get; }
    DbSet<ClinicSetup> Clinics { get; }
    
    DbSet<SSAS.HIS.Domain.Entities.InPatient.AdmitPatients> AdmitPatientss_InPatient { get; }
    DbSet<SSAS.HIS.Domain.Entities.InPatient.Bed> Beds_InPatient { get; }
    DbSet<SSAS.HIS.Domain.Entities.OutPatient.ClinicSchedule> ClinicSchedules_OutPatient { get; }
    DbSet<SSAS.HIS.Domain.Entities.InPatient.MedicalObservation> MedicalObservations_InPatient { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
