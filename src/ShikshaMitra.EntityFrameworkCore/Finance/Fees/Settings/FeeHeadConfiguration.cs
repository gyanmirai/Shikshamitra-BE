using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Fees.Settings;

public class FeeHeadConfiguration : IEntityTypeConfiguration<FeeHead>
{
	public void Configure(EntityTypeBuilder<FeeHead> builder)
	{
		builder.ToTable("Finance_FeeHeads", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.Name).HasMaxLength(40).IsRequired();
		builder.Property(x => x.Module).HasConversion<string>().HasMaxLength(50).IsRequired(false);
		builder.Property(x => x.Description).HasMaxLength(500);
		builder.Property(x => x.DeactivationReason).HasMaxLength(500);

		// add navigation for fee category
		builder.HasOne(fh => fh.FeeCategory)
			.WithMany()
			.HasForeignKey(fh => fh.FeeCategoryId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.Navigation(fh => fh.FeeCategory)
			.AutoInclude();

		builder.HasOne(fh => fh.IncomeCategory)
			.WithMany()
			.HasForeignKey(fh => fh.IncomeCategoryId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.FeeCategoryId, x.Name });
		builder.HasIndex(x => x.IncomeCategoryId);
	}
}
