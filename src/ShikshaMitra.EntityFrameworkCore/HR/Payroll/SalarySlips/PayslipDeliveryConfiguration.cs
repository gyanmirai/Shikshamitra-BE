using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public class PayslipDeliveryConfiguration : IEntityTypeConfiguration<PayslipDelivery>
{
	public void Configure(EntityTypeBuilder<PayslipDelivery> builder)
	{
		builder.ToTable("HR_PayslipDeliveries");
		builder.ConfigureByConvention();
		builder.Property(x => x.StartedAt).HasColumnType("timestamp with time zone");
		builder.Property(x => x.CompletedAt).HasColumnType("timestamp with time zone");
		builder.Property(x => x.CreationTime).HasColumnType("timestamp with time zone");
		builder.Property(x => x.LastModificationTime).HasColumnType("timestamp with time zone");
		builder.Property(x => x.DeletionTime).HasColumnType("timestamp with time zone");
		builder.Property(x => x.Recipient).HasMaxLength(254);
		builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
		builder.Property(x => x.ErrorCategory).HasMaxLength(64);
		builder.Property(x => x.RequiredPermission).IsRequired().HasMaxLength(200);
		builder.HasIndex(x => new { x.DocumentId, x.RequestId }).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.DocumentId, x.CreationTime });
	}
}