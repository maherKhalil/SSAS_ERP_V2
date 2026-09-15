using System.Threading;
using System.Threading.Tasks;
using SSAS.HIS.Application.Registration.Patients;
using SSAS.HIS.Domain.Entities.Registration;

namespace SSAS.HIS.Infrastructure.Persistence;

public sealed class PatientRepository(IHisDbContext context) : IPatientRepository
{
    public void Add(Patient patient) => context.Patients.Add(patient);
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => context.SaveChangesAsync(cancellationToken);
}
