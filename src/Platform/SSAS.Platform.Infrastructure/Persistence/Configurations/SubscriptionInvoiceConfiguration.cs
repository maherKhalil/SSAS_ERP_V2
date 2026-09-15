using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAS.Platform.Domain.Subscriptions;
using SSAS.BuildingBlocks.Infrastructure.Persistence;

namespace SSAS.Platform.Infrastructure.Persistence.Configurations;

public class SubscriptionInvoiceConfiguration : IEntityTypeConfiguration<SubscriptionInvoice>
{
  public void Configure(EntityTypeBuilder<SubscriptionInvoice> builder)
  {
    builder.ToTable("SubscriptionInvoices");
    builder.HasKey(x => x.Id);

    builder.Property(x => x.TenantId).IsRequired();
    builder.Property(x => x.CurrencyCode).IsRequired().HasMaxLength(3);
    builder.Property(x => x.IssuedUtc).IsRequired();
    builder.Property(x => x.InvoiceNumber).HasMaxLength(50);
    builder.Property(x => x.State).HasConversion<string>().HasMaxLength(20);

    builder.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.SubscriptionInvoiceId).OnDelete(DeleteBehavior.Cascade);
  }
}

public class SubscriptionInvoiceLineConfiguration : IEntityTypeConfiguration<SubscriptionInvoiceLine>
{
  public void Configure(EntityTypeBuilder<SubscriptionInvoiceLine> builder)
  {
    builder.ToTable("SubscriptionInvoiceLines");
    builder.HasKey(x => x.Id);

    builder.Property(x => x.SubscriptionInvoiceId).IsRequired();
    builder.Property(x => x.TenantSubscriptionId).IsRequired();
    builder.Property(x => x.Amount).HasColumnType("decimal(19,4)").IsRequired();
    builder.Property(x => x.Description).IsRequired().HasMaxLength(500);
  }
}

public class SubscriptionPaymentAttemptConfiguration : IEntityTypeConfiguration<SubscriptionPaymentAttempt>
{
  public void Configure(EntityTypeBuilder<SubscriptionPaymentAttempt> builder)
  {
    builder.ToTable("SubscriptionPaymentAttempts");
    builder.HasKey(x => x.Id);

    builder.Property(x => x.SubscriptionInvoiceId).IsRequired();
    builder.Property(x => x.AttemptedUtc).IsRequired();
    builder.Property(x => x.Outcome).IsRequired().HasMaxLength(50);
    builder.Property(x => x.ProviderReference).IsRequired().HasMaxLength(100);
  }
}
