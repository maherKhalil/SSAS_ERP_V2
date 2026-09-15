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
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
