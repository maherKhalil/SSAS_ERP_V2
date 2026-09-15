using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.HR.Domain.EmployeeDocuments;

namespace SSAS.HR.Infrastructure.Persistence;

public sealed class EmployeeDocumentContentConfiguration : IEntityTypeConfiguration<EmployeeDocumentContent>
{
  public void Configure(EntityTypeBuilder<EmployeeDocumentContent> builder)
  {
    builder.ToTable("EmployeeDocumentContents", "tenant");

    builder.HasKey(x => x.DocumentId);
    
    builder.Property(x => x.TenantId).IsRequired();

    builder.Property(x => x.Content).HasColumnType("varbinary(max)").IsRequired();

    builder.HasOne(typeof(EmployeeDocument))
      .WithMany()
      .HasForeignKey(nameof(EmployeeDocumentContent.DocumentId))
      .OnDelete(DeleteBehavior.Cascade);
  }
}
