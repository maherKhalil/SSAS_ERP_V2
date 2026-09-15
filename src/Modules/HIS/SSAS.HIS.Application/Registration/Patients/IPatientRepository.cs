using System;
using System.Threading;
using System.Threading.Tasks;
using SSAS.HIS.Domain.Entities.Registration;

namespace SSAS.HIS.Application.Registration.Patients;

public interface IPatientRepository
{
    void Add(Patient patient);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
