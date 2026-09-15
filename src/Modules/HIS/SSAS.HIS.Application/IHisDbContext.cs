using Microsoft.EntityFrameworkCore;
using SSAS.HIS.Domain.Entities.Registration;

namespace SSAS.HIS.Application;

public interface IHisDbContext
{
    DbSet<Patient> Patients { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
