using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using Volo.Abp.EntityFrameworkCore.Modeling;


public class ConcessionTypeConfiguration : IEntityTypeConfiguration<ConcessionType>
{
	public void Configure(EntityTypeBuilder<ConcessionType> builder)
	{
		builder.ToTable("Finance_ConcessionTypes", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Name).HasMaxLength(ConcessionTypeConsts.MaxNameLength).IsRequired();
		builder.Property(x => x.NormalizedName).HasMaxLength(ConcessionTypeConsts.MaxNameLength).IsRequired();
		builder.Property(x => x.Description).HasMaxLength(ConcessionTypeConsts.MaxDescriptionLength);
		builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
		builder.Property(x => x.DeactivationReason).HasMaxLength(ConcessionTypeConsts.MaxDeactivationReasonLength);
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.NormalizedName })
			.IsUnique()
			.HasFilter("\"IsDeleted\" = false")
			.HasDatabaseName("UX_Finance_ConcessionTypes_Tenant_Year_NormalizedName");

		builder.Navigation(x => x.ConcessionTypeDocuments).AutoInclude();
	}
}
