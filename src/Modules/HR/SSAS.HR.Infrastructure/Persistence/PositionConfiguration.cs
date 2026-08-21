using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.HR.Domain.Positions;

namespace SSAS.HR.Infrastructure.Persistence;

// Position persistence in the tenant ERP database (FP-008 data-model, ADR-017).
//
// OWNED BY HR, APPLIED TO PLATFORM'S CONTEXT, exactly as `EmployeeConfiguration` and
// `DepartmentConfiguration` are: HR maps its own entities through `ITenantModelContributor`, so tenant
// business data keeps ONE context and ONE migration stream without either module referencing the other.
//
// ================================================================================================
// THERE IS NO BranchId COLUMN, AND NO EmployeeId COLUMN, AND NO DepartmentId COLUMN.
// ================================================================================================
//
// Each absence is a decision with its own reasoning and its own guard:
//
//   BranchId     — `DEC-POS-0001`. A position spans the branches of its company; a branch-owned position
//                  would be stranded by every `ADR-024` transfer.
//   EmployeeId   — `DEC-POS-0002`. `Employee.PositionId -> Position` plus any reverse key is a CYCLE in the
//                  foreign-key graph, and `TenantCutoverCopyPlan.Order` returns
//                  `CutoverCopyOrderUndecidable` on a cycle. Shared→Dedicated cutover would stop working for
//                  every tenant, without degrading or warning.
//   DepartmentId — `OD-POS-003`. `Employee.DepartmentId` is the single authority on an employee's
//                  department; a copy here would be a second source of truth for the same fact.
//
// Architecture guards assert all three against the COMPOSED MODEL, not against these files, so a shadow
// property or a future convention cannot add one silently.
public sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
  public void Configure(EntityTypeBuilder<Position> builder)
  {
    ArgumentNullException.ThrowIfNull(builder);

    builder.ToTable("Positions", EmployeeConfiguration.TenantSchema, table =>
    {
      table.HasCheckConstraint("CK_Positions_Status", "[Status] IN (N'Active', N'Inactive')");
      table.HasCheckConstraint("CK_Positions_Code_NotBlank", "LEN(LTRIM(RTRIM([Code]))) > 0");
      table.HasCheckConstraint("CK_Positions_Title_NotBlank", "LEN(LTRIM(RTRIM([Title]))) > 0");
    });

    builder.HasKey(position => position.Id);
    builder.Property(position => position.Id).HasColumnName("PositionId").ValueGeneratedNever();
    builder.Ignore(position => position.PositionId);

    // ---- THE TWO OWNERSHIP DIMENSIONS. There is deliberately no third.
    builder.Property(position => position.TenantId).IsRequired();
    builder.Property(position => position.CompanyId).IsRequired();

    builder.Property(position => position.Code)
      .HasConversion(code => code.Value, value => PositionCode.Create(value).Value)
      .HasMaxLength(PositionCode.MaximumLength)
      .IsRequired();
    builder.Property(position => position.NormalizedCode)
      .HasField("normalizedCode")
      .UsePropertyAccessMode(PropertyAccessMode.Field)
      .HasMaxLength(PositionCode.MaximumLength)
      .UseCollation(EmployeeConfiguration.OrdinalCollation)
      .IsRequired();

    builder.Property(position => position.Title)
      .HasConversion(title => title.Value, value => PositionTitle.Create(value).Value)
      .HasMaxLength(PositionTitle.MaximumLength)
      .IsRequired();

    // NULL MEANS UNGRADED. A position may exist before it is placed on the ladder.
    builder.Property(position => position.JobGradeId);

    builder.Property(position => position.Status)
      .HasConversion<string>()
      .HasMaxLength(32)
      .UseCollation(EmployeeConfiguration.OrdinalCollation)
      .IsRequired();
    builder.Property(position => position.StatusChangedUtc).IsRequired();
    builder.Property(position => position.StatusChangedBy)
      .HasMaxLength(Position.ActorMaximumLength)
      .IsRequired();

    builder.Property(position => position.CreatedUtc).IsRequired();
    builder.Property(position => position.CreatedBy).HasMaxLength(Position.ActorMaximumLength);
    builder.Property(position => position.ModifiedUtc).IsRequired();
    builder.Property(position => position.ModifiedBy).HasMaxLength(Position.ActorMaximumLength);

    builder.Property(position => position.RowVersion).IsRowVersion().IsConcurrencyToken();

    // ---- CODE IS UNIQUE WITHIN A COMPANY, AND NOTHING ELSE PARTICIPATES.
    //
    // Per COMPANY is the whole scope: `OD-POS-003` ruled Position independent of Department, so there is no
    // per-department variant. Binary collation makes the index authoritative under concurrent creation
    // rather than merely advisory, which is why codes that normalize alike collide.
    builder.HasIndex(position => new
      {
        position.TenantId, position.CompanyId, position.NormalizedCode
      })
      .IsUnique()
      .HasDatabaseName("UX_Positions_TenantId_CompanyId_NormalizedCode");

    // Scoped list. Leading keys match the mandatory predicate order, so no scoped read can be served by a
    // scan that ignores a scope column (`NFR-POS-0301`).
    builder.HasIndex(position => new
      {
        position.TenantId, position.CompanyId, position.Status
      })
      .HasDatabaseName("IX_Positions_TenantId_CompanyId_Status");

    // Grade filter, and the dependent lookup `DEC-POS-0013` needs to refuse deactivating a grade that
    // active positions still reference.
    builder.HasIndex(position => new
      {
        position.TenantId, position.CompanyId, position.JobGradeId
      })
      .HasDatabaseName("IX_Positions_TenantId_CompanyId_JobGradeId");

    // ---- THE GRADE REFERENCE, RESTRICTED.
    //
    // Grades are deactivated, never deleted, so RESTRICT costs nothing and buys real integrity. No
    // navigation property is declared: a caller that could walk from a position to its grade would be a read
    // that bypasses the grade's own scope resolution.
    builder.HasOne<JobGrade>()
      .WithMany()
      .HasForeignKey(position => position.JobGradeId)
      .OnDelete(DeleteBehavior.Restrict);

    // The foreign key to Company is declared in HrTenantModelContributor, by PRINCIPAL TYPE NAME: HR cannot
    // reference Platform's Company type, so the typed relationship API is not available here.
  }
}
