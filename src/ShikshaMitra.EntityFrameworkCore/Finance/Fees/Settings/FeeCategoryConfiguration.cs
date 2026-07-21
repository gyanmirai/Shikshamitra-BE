using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Fees.Settings;

public class FeeCategoryConfiguration : IEntityTypeConfiguration<FeeCategory>
{
	public void Configure(EntityTypeBuilder<FeeCategory> builder)
	{
		builder.ToTable("Finance_FeeCategories", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
		builder.Property(x => x.Description).HasMaxLength(500);
		builder.Property(x => x.DeactivationReason).HasMaxLength(500);

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Name });
	}
}
