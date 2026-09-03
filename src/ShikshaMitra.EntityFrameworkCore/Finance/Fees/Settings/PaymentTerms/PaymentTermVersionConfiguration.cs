using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.Settings.PaymentTerms;

public class PaymentTermVersionConfiguration : IEntityTypeConfiguration<PaymentTermVersion>
{
	public void Configure(EntityTypeBuilder<PaymentTermVersion> builder)
	{
		builder.ToTable("Finance_PaymentTermVersions", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.Status).HasConversion<int>();
		builder.Property(x => x.HolidayShiftingMethod).HasConversion<int>();
		builder.Property(x => x.DemandGenerationRule).HasConversion<int>();
		builder.Property(x => x.ChangeReason).HasMaxLength(500);
		builder.Property(x => x.SnapshotHash).HasMaxLength(128);

		builder.HasIndex(x => new { x.TenantId, x.PaymentTermId, x.VersionNumber }).IsUnique();

		builder.HasMany(x => x.Installments)
			.WithOne()
			.HasForeignKey(x => x.PaymentTermVersionId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.Navigation(x => x.Installments).AutoInclude();
	}
}
