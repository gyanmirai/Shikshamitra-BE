using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.Settings.PaymentTerms;

public class PaymentTermInstallmentConfiguration : IEntityTypeConfiguration<PaymentTermInstallment>
{
	public void Configure(EntityTypeBuilder<PaymentTermInstallment> builder)
	{
		builder.ToTable("Finance_PaymentTermInstallments", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
		builder.Property(x => x.AllocationPercentage).HasPrecision(18, 4);

		builder.HasIndex(x => new { x.PaymentTermVersionId, x.Sequence }).IsUnique();
	}
}
