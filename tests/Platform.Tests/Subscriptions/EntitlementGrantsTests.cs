using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;
using FluentAssertions;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Application.Abstractions.Identity;
using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Domain.ValueObjects;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Subscriptions.EntitlementGrants;
using SSAS.Platform.Application.Subscriptions;

namespace SSAS.Platform.Tests.Subscriptions;

public class EntitlementGrantsTests
{
  private readonly Mock<IPlatformUnitOfWork> _unitOfWorkMock = new();
  private readonly Mock<ITenantEntitlementGrantRepository> _repositoryMock = new();
  private readonly Mock<ITenantEntitlementReader> _readerMock = new();
  private readonly Mock<ITenantEntitlementCache> _cacheMock = new();
  private readonly Mock<ICurrentUser> _currentUserMock = new();
  private readonly EntitlementGrantsCommandHandler _handler;

  public EntitlementGrantsTests()
  {
    _currentUserMock.Setup(u => u.UserId).Returns("TestUser");
    _handler = new EntitlementGrantsCommandHandler(
      _unitOfWorkMock.Object,
      _repositoryMock.Object,
      _readerMock.Object,
      _cacheMock.Object,
      _currentUserMock.Object
    );
  }

  [Fact]
  public async Task HandleGrantModuleAsync_ShouldAddGrantAndInvalidateCache()
  {
    var tenantId = Guid.NewGuid();
    var command = new GrantModuleCommand(tenantId, "Payroll", DateTimeOffset.UtcNow, null, "123", "Reason");
    
    var result = await _handler.HandleGrantModuleAsync(command, CancellationToken.None);
    
    result.IsSuccess.Should().BeTrue();
    _repositoryMock.Verify(r => r.AddAsync(It.IsAny<TenantEntitlementGrant>(), CancellationToken.None), Times.Once);
    _unitOfWorkMock.Verify(u => u.SaveChangesAsync(CancellationToken.None), Times.Once);
    _cacheMock.Verify(c => c.InvalidateTenant(tenantId), Times.Once);
  }

  [Fact]
  public async Task HandleRaiseLimitAsync_ShouldAddGrantAndInvalidateCache()
  {
    var tenantId = Guid.NewGuid();
    var command = new RaiseLimitCommand(tenantId, "Seats", 100, DateTimeOffset.UtcNow, null, "123", "Reason");
    
    _readerMock.Setup(r => r.ReadAsync(tenantId, CancellationToken.None))
      .ReturnsAsync(new TenantEntitlementSnapshot(tenantId, Guid.NewGuid(), null, new HashSet<string>(), new Dictionary<string, long> { { "Seats", 50 } }, new List<EntitlementGrantFact>()));

    var result = await _handler.HandleRaiseLimitAsync(command, CancellationToken.None);
    
    result.IsSuccess.Should().BeTrue();
    _repositoryMock.Verify(r => r.AddAsync(It.IsAny<TenantEntitlementGrant>(), CancellationToken.None), Times.Once);
    _unitOfWorkMock.Verify(u => u.SaveChangesAsync(CancellationToken.None), Times.Once);
    _cacheMock.Verify(c => c.InvalidateTenant(tenantId), Times.Once);
  }

  [Fact]
  public async Task HandleRevokeModuleAsync_ShouldAddGrantAndInvalidateCache()
  {
    var tenantId = Guid.NewGuid();
    var command = new RevokeModuleCommand(tenantId, "Payroll", DateTimeOffset.UtcNow, "123", "Reason");
    
    var result = await _handler.HandleRevokeModuleAsync(command, CancellationToken.None);
    
    result.IsSuccess.Should().BeTrue();
    _repositoryMock.Verify(r => r.AddAsync(It.IsAny<TenantEntitlementGrant>(), CancellationToken.None), Times.Once);
    _unitOfWorkMock.Verify(u => u.SaveChangesAsync(CancellationToken.None), Times.Once);
    _cacheMock.Verify(c => c.InvalidateTenant(tenantId), Times.Once);
  }
}
