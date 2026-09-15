using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SSAS.BuildingBlocks.Domain;
using SSAS.BuildingBlocks.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Abstractions.Persistence;
using SSAS.Platform.Application.Subscriptions.Invoices;
using SSAS.Platform.Domain.Subscriptions;
using SSAS.Platform.Domain.Enums;
using FluentAssertions;

namespace SSAS.Platform.Tests.Subscriptions;

public sealed class InvoicesCommandHandlerTests
{
  [Fact]
  public async Task HandleCreateAsync_success_adds_invoice_and_saves()
  {
    var repo = new StubInvoiceRepository();
    var uow = new StubUnitOfWork();
    var handler = new InvoicesCommandHandler(repo, uow);
    var command = new CreateInvoiceCommand(Guid.NewGuid(), "USD", DateTimeOffset.UtcNow, new List<CreateInvoiceLineCommand>());

    var result = await handler.HandleCreateAsync(command, CancellationToken.None);

    result.IsSuccess.Should().BeTrue();
    result.Value.Should().NotBeEmpty();
    repo.AddCalled.Should().BeTrue();
    uow.SaveCount.Should().Be(1);
  }

  [Fact]
  public async Task HandleCreateAsync_with_lines_stores_lines()
  {
    var repo = new StubInvoiceRepository();
    var uow = new StubUnitOfWork();
    var handler = new InvoicesCommandHandler(repo, uow);
    var lines = new List<CreateInvoiceLineCommand>
    {
      new(Guid.NewGuid(), 100m, "Service fee"),
      new(Guid.NewGuid(), 50m, "Support fee")
    };
    var command = new CreateInvoiceCommand(Guid.NewGuid(), "EUR", DateTimeOffset.UtcNow, lines);

    var result = await handler.HandleCreateAsync(command, CancellationToken.None);

    result.IsSuccess.Should().BeTrue();
    repo.StoredInvoice!.Lines.Should().HaveCount(2);
  }

  [Fact]
  public async Task HandleUpdateDraftAsync_on_draft_invoice_succeeds()
  {
    var invoice = SubscriptionInvoice.CreateDraft(Guid.NewGuid(), "USD", DateTimeOffset.UtcNow).Value;
    var repo = new StubInvoiceRepository(invoice);
    var uow = new StubUnitOfWork();
    var handler = new InvoicesCommandHandler(repo, uow);
    var command = new UpdateInvoiceDraftCommand(invoice.Id, "GBP", DateTimeOffset.UtcNow.AddDays(1), new List<CreateInvoiceLineCommand>());

    var result = await handler.HandleUpdateDraftAsync(command, CancellationToken.None);

    result.IsSuccess.Should().BeTrue();
    uow.SaveCount.Should().Be(1);
  }

  [Fact]
  public async Task HandleUpdateDraftAsync_on_issued_invoice_returns_NotDraft_failure()
  {
    var invoice = SubscriptionInvoice.CreateDraft(Guid.NewGuid(), "USD", DateTimeOffset.UtcNow).Value;
    invoice.Issue("INV-001");
    var repo = new StubInvoiceRepository(invoice);
    var uow = new StubUnitOfWork();
    var handler = new InvoicesCommandHandler(repo, uow);
    var command = new UpdateInvoiceDraftCommand(invoice.Id, "USD", DateTimeOffset.UtcNow, new List<CreateInvoiceLineCommand>());

    var result = await handler.HandleUpdateDraftAsync(command, CancellationToken.None);

    result.IsFailure.Should().BeTrue();
    result.Error.Code.Should().Be("Invoice.NotDraft");
    uow.SaveCount.Should().Be(0);
  }

  [Fact]
  public async Task HandleUpdateDraftAsync_invoice_not_found_returns_NotFound_failure()
  {
    var repo = new StubInvoiceRepository(null);
    var uow = new StubUnitOfWork();
    var handler = new InvoicesCommandHandler(repo, uow);
    var command = new UpdateInvoiceDraftCommand(Guid.NewGuid(), "USD", DateTimeOffset.UtcNow, new List<CreateInvoiceLineCommand>());

    var result = await handler.HandleUpdateDraftAsync(command, CancellationToken.None);

    result.IsFailure.Should().BeTrue();
    result.Error.Code.Should().Be("Invoice.NotFound");
  }

  [Fact]
  public async Task HandleIssueAsync_on_draft_invoice_issues_with_number()
  {
    var invoice = SubscriptionInvoice.CreateDraft(Guid.NewGuid(), "USD", DateTimeOffset.UtcNow).Value;
    var repo = new StubInvoiceRepository(invoice);
    var uow = new StubUnitOfWork();
    var handler = new InvoicesCommandHandler(repo, uow);
    var command = new IssueInvoiceCommand(invoice.Id, "INV-2026-001");

    var result = await handler.HandleIssueAsync(command, CancellationToken.None);

    result.IsSuccess.Should().BeTrue();
    invoice.State.Should().Be(SubscriptionInvoiceState.Issued);
    invoice.InvoiceNumber.Should().Be("INV-2026-001");
    uow.SaveCount.Should().Be(1);
  }

  [Fact]
  public async Task HandleIssueAsync_on_already_issued_returns_NotDraft_failure()
  {
    var invoice = SubscriptionInvoice.CreateDraft(Guid.NewGuid(), "USD", DateTimeOffset.UtcNow).Value;
    invoice.Issue("INV-FIRST");
    var repo = new StubInvoiceRepository(invoice);
    var uow = new StubUnitOfWork();
    var handler = new InvoicesCommandHandler(repo, uow);
    var command = new IssueInvoiceCommand(invoice.Id, "INV-SECOND");

    var result = await handler.HandleIssueAsync(command, CancellationToken.None);

    result.IsFailure.Should().BeTrue();
    result.Error.Code.Should().Be("Invoice.NotDraft");
    uow.SaveCount.Should().Be(0);
  }

  [Fact]
  public async Task HandleIssueAsync_invoice_not_found_returns_NotFound_failure()
  {
    var repo = new StubInvoiceRepository(null);
    var uow = new StubUnitOfWork();
    var handler = new InvoicesCommandHandler(repo, uow);
    var command = new IssueInvoiceCommand(Guid.NewGuid(), "INV-001");

    var result = await handler.HandleIssueAsync(command, CancellationToken.None);

    result.IsFailure.Should().BeTrue();
    result.Error.Code.Should().Be("Invoice.NotFound");
  }

  [Fact]
  public async Task HandleVoidAsync_on_draft_invoice_voids_it()
  {
    var invoice = SubscriptionInvoice.CreateDraft(Guid.NewGuid(), "USD", DateTimeOffset.UtcNow).Value;
    var repo = new StubInvoiceRepository(invoice);
    var uow = new StubUnitOfWork();
    var handler = new InvoicesCommandHandler(repo, uow);
    var command = new VoidInvoiceCommand(invoice.Id);

    var result = await handler.HandleVoidAsync(command, CancellationToken.None);

    result.IsSuccess.Should().BeTrue();
    invoice.State.Should().Be(SubscriptionInvoiceState.Voided);
    uow.SaveCount.Should().Be(1);
  }

  [Fact]
  public async Task HandleVoidAsync_on_already_voided_returns_failure()
  {
    var invoice = SubscriptionInvoice.CreateDraft(Guid.NewGuid(), "USD", DateTimeOffset.UtcNow).Value;
    invoice.Void();
    var repo = new StubInvoiceRepository(invoice);
    var uow = new StubUnitOfWork();
    var handler = new InvoicesCommandHandler(repo, uow);
    var command = new VoidInvoiceCommand(invoice.Id);

    var result = await handler.HandleVoidAsync(command, CancellationToken.None);

    result.IsFailure.Should().BeTrue();
    result.Error.Code.Should().Be("Invoice.AlreadyVoided");
  }

  [Fact]
  public async Task HandleVoidAsync_invoice_not_found_returns_NotFound_failure()
  {
    var repo = new StubInvoiceRepository(null);
    var uow = new StubUnitOfWork();
    var handler = new InvoicesCommandHandler(repo, uow);
    var command = new VoidInvoiceCommand(Guid.NewGuid());

    var result = await handler.HandleVoidAsync(command, CancellationToken.None);

    result.IsFailure.Should().BeTrue();
    result.Error.Code.Should().Be("Invoice.NotFound");
  }

  [Fact]
  public async Task QueryHandler_GetInvoices_delegates_to_queries()
  {
    var queries = new StubInvoiceQueries();
    var handler = new InvoicesQueryHandler(queries);

    var result = await handler.HandleAsync(new GetInvoicesQuery(), CancellationToken.None);

    result.IsSuccess.Should().BeTrue();
    queries.GetInvoicesCallCount.Should().Be(1);
  }

  [Fact]
  public async Task QueryHandler_GetInvoiceById_delegates_with_correct_id()
  {
    var queries = new StubInvoiceQueries();
    var handler = new InvoicesQueryHandler(queries);
    var id = Guid.NewGuid();

    var result = await handler.HandleAsync(new GetInvoiceByIdQuery(id), CancellationToken.None);

    result.IsSuccess.Should().BeTrue();
    queries.LastInvoiceId.Should().Be(id);
  }

  [Fact]
  public async Task QueryHandler_GetTenantInvoices_delegates_with_correct_tenantId()
  {
    var queries = new StubInvoiceQueries();
    var handler = new InvoicesQueryHandler(queries);
    var tenantId = Guid.NewGuid();

    var result = await handler.HandleAsync(new GetTenantInvoicesQuery(tenantId), CancellationToken.None);

    result.IsSuccess.Should().BeTrue();
    queries.LastTenantId.Should().Be(tenantId);
  }

  [Fact]
  public async Task QueryHandler_GetInvoiceAttempts_delegates_with_correct_invoiceId()
  {
    var queries = new StubInvoiceQueries();
    var handler = new InvoicesQueryHandler(queries);
    var invoiceId = Guid.NewGuid();

    var result = await handler.HandleAsync(new GetInvoiceAttemptsQuery(invoiceId), CancellationToken.None);

    result.IsSuccess.Should().BeTrue();
    queries.LastAttemptsInvoiceId.Should().Be(invoiceId);
  }

  private sealed class StubInvoiceRepository : ISubscriptionInvoiceRepository
  {
    private readonly SubscriptionInvoice? _seeded;
    public StubInvoiceRepository(SubscriptionInvoice? seeded = null) { _seeded = seeded; }
    public bool AddCalled { get; private set; }
    public SubscriptionInvoice? StoredInvoice { get; private set; }
    public Task<SubscriptionInvoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
      => Task.FromResult(_seeded);
    public void Add(SubscriptionInvoice invoice) { AddCalled = true; StoredInvoice = invoice; }
  }

  private sealed class StubUnitOfWork : IPlatformUnitOfWork
  {
    public int SaveCount { get; private set; }
    public Task<Result<int>> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      SaveCount++;
      return Task.FromResult(Result.Success(1));
    }
    public Task<ITransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
      => Task.FromResult<ITransaction>(null!);
  }

  private sealed class StubInvoiceQueries : ISubscriptionInvoiceQueries
  {
    public int GetInvoicesCallCount { get; private set; }
    public Guid? LastInvoiceId { get; private set; }
    public Guid? LastTenantId { get; private set; }
    public Guid? LastAttemptsInvoiceId { get; private set; }

    public Task<Result<IReadOnlyCollection<InvoiceDto>>> GetInvoicesAsync(CancellationToken cancellationToken = default)
    {
      GetInvoicesCallCount++;
      return Task.FromResult(Result.Success<IReadOnlyCollection<InvoiceDto>>(new List<InvoiceDto>()));
    }

    public Task<Result<InvoiceDto>> GetInvoiceByIdAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
      LastInvoiceId = invoiceId;
      return Task.FromResult(Result.Success(new InvoiceDto(invoiceId, null, Guid.NewGuid(), "USD", DateTimeOffset.UtcNow, SubscriptionInvoiceState.Draft, new List<InvoiceLineDto>())));
    }

    public Task<Result<IReadOnlyCollection<InvoiceDto>>> GetTenantInvoicesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
      LastTenantId = tenantId;
      return Task.FromResult(Result.Success<IReadOnlyCollection<InvoiceDto>>(new List<InvoiceDto>()));
    }

    public Task<Result<IReadOnlyCollection<PaymentAttemptDto>>> GetInvoiceAttemptsAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
      LastAttemptsInvoiceId = invoiceId;
      return Task.FromResult(Result.Success<IReadOnlyCollection<PaymentAttemptDto>>(new List<PaymentAttemptDto>()));
    }
  }
}
