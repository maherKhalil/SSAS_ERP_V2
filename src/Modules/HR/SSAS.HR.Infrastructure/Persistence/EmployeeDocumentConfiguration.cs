using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.HR.Domain.EmployeeDocuments;

namespace SSAS.HR.Infrastructure.Persistence;

public sealed class EmployeeDocumentConfiguration : IEntityTypeConfiguration<EmployeeDocument>
{
  public void Configure(EntityTypeBuilder<EmployeeDocument> builder)
  {
    builder.ToTable("EmployeeDocuments", "tenant");

    builder.HasKey(x => x.Id);
    builder.Property(x => x.Id)
      .HasColumnName("DocumentId")
      .ValueGeneratedNever();

    builder.Property(x => x.TenantId).IsRequired();
    builder.Property(x => x.CompanyId).IsRequired();
    builder.Property(x => x.EmployeeId).IsRequired();

    builder.Property(x => x.DocumentType)
      .HasMaxLength(32)
      .HasConversion<string>()
      .IsRequired();

    builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
    builder.Property(x => x.NormalizedFileName).HasMaxLength(260).IsRequired();
    builder.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
    builder.Property(x => x.ByteCount).IsRequired();

    builder.Property(x => x.ContentHash).HasColumnType("binary(32)").IsRequired();
    
    builder.Property(x => x.ContentLocation).HasMaxLength(512);

    builder.Property(x => x.Status)
      .HasMaxLength(32)
      .HasConversion<string>()
      .IsRequired();

    builder.Property(x => x.RowVersion)
      .IsRowVersion()
      .IsRequired();

    builder.HasIndex(x => new { x.TenantId, x.CompanyId, x.EmployeeId, x.Status })
      .HasDatabaseName("IX_EmployeeDocuments_Employee");
      
    builder.HasOne(typeof(SSAS.HR.Domain.Employees.Employee))
      .WithMany()
      .HasForeignKey(nameof(EmployeeDocument.EmployeeId))
      .OnDelete(DeleteBehavior.Restrict);
  }
}
