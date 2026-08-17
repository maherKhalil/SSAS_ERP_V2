using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.TenantStorage;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public sealed class TenantCutoverOperationConfiguration
  : IEntityTypeConfiguration<TenantCutoverOperation>
{
  public void Configure(EntityTypeBuilder<TenantCutoverOperation> builder)
  {
    builder.ToTable("TenantCutoverOperations", PlatformPersistenceConstants.Schema, table =>
    {
      table.HasCheckConstraint(
        "CK_TenantCutoverOperations_Status",
        "[Status] IN (N'Preparing', N'Frozen', N'RoutingFlipped', N'Completed', N'Abandoned')");

      // A cutover never moves a tenant onto the database it is already on.
      table.HasCheckConstraint(
        "CK_TenantCutoverOperations_DistinctEndpoints",
        "[SourceTenantDatabaseId] <> [TargetTenantDatabaseId]");

      // A FROZEN OPERATION MUST SAY WHEN. The freeze window's start is the fact an operator needs during an
      // incident, and a Frozen row without it would be a fence nobody could reason about.
      table.HasCheckConstraint(
        "CK_TenantCutoverOperations_FrozenHasTimestamp",
        "[Status] <> N'Frozen' OR [FrozenUtc] IS NOT NULL");

      // Routing cannot have moved without a version to move it to. Enforced now so the later flip slice
      // cannot record half the fact (ADR-020 mapping switch).
      table.HasCheckConstraint(
        "CK_TenantCutoverOperations_FlipRecordsVersion",
        "[RoutingFlippedUtc] IS NULL OR [RoutingVersion] IS NOT NULL");

      table.HasCheckConstraint(
        "CK_TenantCutoverOperations_FlippedHasFlipTimestamp",
        "[Status] NOT IN (N'RoutingFlipped', N'Completed') OR [RoutingFlippedUtc] IS NOT NULL");

      // A post-cutover write cannot precede the cutover that made the target authoritative.
      table.HasCheckConstraint(
        "CK_TenantCutoverOperations_PostCutoverWriteFollowsFlip",
        "[PostCutoverWriteObservedUtc] IS NULL OR " +
        "([RoutingFlippedUtc] IS NOT NULL AND [PostCutoverWriteObservedUtc] >= [RoutingFlippedUtc])");
    });

    builder.HasKey(operation => operation.Id);
    builder.Property(operation => operation.Id)
      .HasColumnName("TenantCutoverOperationId")
      .UseIdentityColumn();

    builder.Property(operation => operation.TenantId).IsRequired();
    builder.Property(operation => operation.SourceTenantDatabaseId).IsRequired();
    builder.Property(operation => operation.TargetTenantDatabaseId).IsRequired();

    // Enum-as-string, matching the backup and verification run precedent: the check constraints above are
    // expressible only against the text, and a readable row beats a byte.
    builder.Property(operation => operation.Status)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(PlatformPersistenceConstants.OrdinalCollation)
      .IsRequired();

    builder.Property(operation => operation.StartedUtc).IsRequired();
    builder.Property(operation => operation.FreezeRequestedUtc);
    builder.Property(operation => operation.FrozenUtc);
    builder.Property(operation => operation.FreezeReleasedUtc);
    builder.Property(operation => operation.RoutingFlippedUtc);
    builder.Property(operation => operation.RoutingVersion);
    builder.Property(operation => operation.PostCutoverWriteObservedUtc);
    builder.Property(operation => operation.CompletedUtc);

    builder.Property(operation => operation.FailureSummary)
      .HasMaxLength(TenantCutoverOperation.FailureSummaryMaximumLength);

    // ---- AT MOST ONE ACTIVE CUTOVER PER TENANT, ENFORCED BY THE DATABASE.
    //
    // Two instances that both decide a tenant should be promoted will both INSERT; SQL Server admits
    // exactly one and the other receives a duplicate-key violation it reports as a controlled refusal. A
    // claim on an existing row could not do this, because each instance would create its own — the same
    // reasoning the restore-verification admission index rests on.
    builder.HasIndex(operation => operation.TenantId)
      .HasDatabaseName("UX_TenantCutoverOperations_ActiveTenant")
      .IsUnique()
      .HasFilter("[Status] IN (N'Preparing', N'Frozen', N'RoutingFlipped')");

    // THE WRITE-PATH INDEX (TS-Storage Phase E5). Every tenant write asks "which cutover, if any, governs
    // this tenant's route?", and since E5 that question includes COMPLETED operations — a context bound to a
    // database the tenant was moved off must stay refused long after orchestration finished.
    //
    // The unique index above cannot answer it: its filter excludes Completed by design, so a query spanning
    // Completed would scan the whole table, and cutover history is precisely the part that accumulates.
    // This one is filtered to exactly the statuses the fence asks about and ordered so the newest operation
    // — the one describing where the tenant is now — is the first row of the seek.
    builder.HasIndex(operation => new { operation.TenantId, operation.Id })
      .HasDatabaseName("IX_TenantCutoverOperations_WriteGate")
      .HasFilter("[Status] IN (N'Frozen', N'RoutingFlipped', N'Completed')");
    // Restrict, like backup and verification history: a cutover record is the evidence of when a tenant's
    // authoritative database changed, and the moment a row would be removed is when that matters most.
    builder.HasOne<TenantDatabase>()
      .WithMany()
      .HasForeignKey(operation => operation.SourceTenantDatabaseId)
      .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne<TenantDatabase>()
      .WithMany()
      .HasForeignKey(operation => operation.TargetTenantDatabaseId)
      .OnDelete(DeleteBehavior.Restrict);

    builder.Property(operation => operation.CreatedUtc).IsRequired();
    builder.Property(operation => operation.CreatedBy)
      .HasMaxLength(TenantCutoverOperation.ActorMaximumLength);
    builder.Property(operation => operation.ModifiedUtc).IsRequired();
    builder.Property(operation => operation.ModifiedBy)
      .HasMaxLength(TenantCutoverOperation.ActorMaximumLength);
    builder.Property(operation => operation.RowVersion).IsRowVersion().IsConcurrencyToken();
  }
}
