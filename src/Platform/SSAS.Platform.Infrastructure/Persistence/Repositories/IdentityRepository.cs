using Microsoft.EntityFrameworkCore;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Domain.ValueObjects;
using PlatformIdentity = SSAS.Platform.Domain.Identities.Identity;

namespace SSAS.Platform.Infrastructure.Persistence.Repositories;

public sealed class IdentityRepository(PlatformDbContext dbContext) : IIdentityRepository
{
  public Task<PlatformIdentity?> GetByIdAsync(long identityId, CancellationToken cancellationToken = default) =>
    dbContext.Identities.SingleOrDefaultAsync(identity => identity.Id == identityId, cancellationToken);

  public async Task<PlatformIdentity?> GetBySubjectAsync(string subject, CancellationToken cancellationToken = default)
  {
    var authenticationSubject = AuthenticationSubject.Create(subject);
    return authenticationSubject.IsFailure
      ? null
      : await dbContext.Identities.SingleOrDefaultAsync(
        identity => identity.Subject == authenticationSubject.Value,
        cancellationToken);
  }

  public async Task<bool> SubjectExistsAsync(string subject, CancellationToken cancellationToken = default)
  {
    var authenticationSubject = AuthenticationSubject.Create(subject);
    return authenticationSubject.IsSuccess && await dbContext.Identities.AnyAsync(
      identity => identity.Subject == authenticationSubject.Value,
      cancellationToken);
  }

  public async Task AddAsync(PlatformIdentity identity, CancellationToken cancellationToken = default)
  {
    await dbContext.Identities.AddAsync(identity, cancellationToken);
  }
}
